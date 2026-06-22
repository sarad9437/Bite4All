using System.Security.Cryptography;
using System.Text;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bite4All.API.Middleware;

public class IdempotencyMiddleware(RequestDelegate next)
{
    private static readonly TimeSpan RecordLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan PendingGracePeriod = TimeSpan.FromMinutes(10);

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method) || !context.Request.Headers.TryGetValue("Idempotency-Key", out var key))
        {
            await next(context);
            return;
        }

        var unitOfWork = context.RequestServices.GetRequiredService<IUnitOfWork>();
        var routeKey = $"{context.Request.Method}:{context.Request.Path}:{key}";
        var requestHash = await ComputeRequestHashAsync(context);
        var now = DateTime.UtcNow;

        var existing = await unitOfWork.IdempotencyRecords.Query()
            .FirstOrDefaultAsync(r => r.RouteKey == routeKey, context.RequestAborted);

        if (existing is not null)
        {
            if (existing.ExpiresAtUtc <= now)
            {
                unitOfWork.IdempotencyRecords.Delete(existing);
                await unitOfWork.SaveChangesAsync(context.RequestAborted);
            }
            else if (!string.Equals(existing.RequestHash, requestHash, StringComparison.OrdinalIgnoreCase))
            {
                await WriteConflictAsync(context, "Request with this Idempotency-Key was already processed for a different payload.");
                return;
            }
            else if (existing.StatusCode.HasValue)
            {
                await ReplayResponseAsync(context, existing);
                return;
            }
            else if (existing.CreatedAtUtc.Add(PendingGracePeriod) > now)
            {
                await WriteConflictAsync(context, "Request with this Idempotency-Key is still being processed.");
                return;
            }
            else
            {
                unitOfWork.IdempotencyRecords.Delete(existing);
                await unitOfWork.SaveChangesAsync(context.RequestAborted);
            }
        }

        var pendingRecord = new IdempotencyRecord
        {
            RouteKey = routeKey,
            RequestHash = requestHash,
            ExpiresAtUtc = now.Add(RecordLifetime)
        };

        await unitOfWork.IdempotencyRecords.AddAsync(pendingRecord, context.RequestAborted);

        try
        {
            await unitOfWork.SaveChangesAsync(context.RequestAborted);
        }
        catch (DbUpdateException)
        {
            var conflicted = await unitOfWork.IdempotencyRecords.Query()
                .FirstOrDefaultAsync(r => r.RouteKey == routeKey, context.RequestAborted);

            if (conflicted is not null && string.Equals(conflicted.RequestHash, requestHash, StringComparison.OrdinalIgnoreCase) && conflicted.StatusCode.HasValue)
            {
                await ReplayResponseAsync(context, conflicted);
                return;
            }

            await WriteConflictAsync(context, "Request with this Idempotency-Key is already being processed.");
            return;
        }

        var originalBody = context.Response.Body;
        await using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

        try
        {
            await next(context);
        }
        catch
        {
            context.Response.Body = originalBody;
            unitOfWork.IdempotencyRecords.Delete(pendingRecord);
            await unitOfWork.SaveChangesAsync(context.RequestAborted);
            throw;
        }

        context.Response.Body = originalBody;

        if (context.Response.StatusCode is >= 200 and < 300)
        {
            responseBuffer.Position = 0;
            var responseBody = await new StreamReader(responseBuffer, Encoding.UTF8, leaveOpen: true).ReadToEndAsync();

            pendingRecord.StatusCode = context.Response.StatusCode;
            pendingRecord.ResponseContentType = context.Response.ContentType;
            pendingRecord.ResponseBody = responseBody;
            pendingRecord.ProcessedAtUtc = DateTime.UtcNow;
            pendingRecord.ExpiresAtUtc = DateTime.UtcNow.Add(RecordLifetime);

            unitOfWork.IdempotencyRecords.Update(pendingRecord);
            await unitOfWork.SaveChangesAsync(context.RequestAborted);

            responseBuffer.Position = 0;
            await responseBuffer.CopyToAsync(originalBody, context.RequestAborted);
            return;
        }

        unitOfWork.IdempotencyRecords.Delete(pendingRecord);
        await unitOfWork.SaveChangesAsync(context.RequestAborted);

        responseBuffer.Position = 0;
        await responseBuffer.CopyToAsync(originalBody, context.RequestAborted);
    }

    private static async Task<string> ComputeRequestHashAsync(HttpContext context)
    {
        context.Request.EnableBuffering();

        using var memoryStream = new MemoryStream();
        await context.Request.Body.CopyToAsync(memoryStream, context.RequestAborted);
        context.Request.Body.Position = 0;

        return Convert.ToHexString(SHA256.HashData(memoryStream.ToArray()));
    }

    private static async Task ReplayResponseAsync(HttpContext context, IdempotencyRecord record)
    {
        context.Response.StatusCode = record.StatusCode ?? StatusCodes.Status200OK;
        context.Response.ContentType = record.ResponseContentType;

        if (!string.IsNullOrEmpty(record.ResponseBody))
        {
            await context.Response.WriteAsync(record.ResponseBody, context.RequestAborted);
        }
    }

    private static async Task WriteConflictAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new { message }, context.RequestAborted);
    }
}
