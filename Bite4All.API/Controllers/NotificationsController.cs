using Bite4All.API.Authorization;
using Bite4All.Application.DTOs.Common;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bite4All.API.Controllers;

[ApiController]
[Authorize]
[Route("notifications")]
public class NotificationsController(IUnitOfWork unitOfWork) : ControllerBase
{
    [HttpGet]
    public ActionResult<PagedResult<Notification>> GetMine(
        [FromQuery] ActorType recipientType,
        [FromQuery] int recipientId,
        [FromQuery] bool? unreadOnly = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!CanAccess(recipientType, recipientId))
        {
            return Forbid();
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = unitOfWork.Notifications.Query()
            .Where(n => n.RecipientType == recipientType && n.RecipientId == recipientId);

        if (unreadOnly == true)
        {
            query = query.Where(n => !n.IsRead);
        }

        var totalCount = query.Count();
        var items = query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new PagedResult<Notification>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    [HttpPut("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id, CancellationToken cancellationToken)
    {
        var notification = await unitOfWork.Notifications.GetByIdAsync(id, cancellationToken);
        if (notification is null)
        {
            return NotFound();
        }

        if (!CanAccess(notification.RecipientType, notification.RecipientId))
        {
            return Forbid();
        }

        notification.IsRead = true;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Marks all notifications for the given actor as read in a single operation.
    /// </summary>
    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead(
        [FromQuery] ActorType recipientType,
        [FromQuery] int recipientId,
        CancellationToken cancellationToken)
    {
        if (!CanAccess(recipientType, recipientId))
        {
            return Forbid();
        }

        var unread = unitOfWork.Notifications.Query()
            .Where(n => n.RecipientType == recipientType && n.RecipientId == recipientId && !n.IsRead)
            .ToList();

        foreach (var notification in unread)
        {
            notification.IsRead = true;
        }

        if (unread.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    /// <summary>
    /// Deletes a single notification. Only the recipient or an admin may delete it.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var notification = await unitOfWork.Notifications.GetByIdAsync(id, cancellationToken);
        if (notification is null)
        {
            return NotFound();
        }

        if (!CanAccess(notification.RecipientType, notification.RecipientId))
        {
            return Forbid();
        }

        unitOfWork.Notifications.Delete(notification);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Deletes all notifications for the given actor (bulk clear).
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> DeleteAll(
        [FromQuery] ActorType recipientType,
        [FromQuery] int recipientId,
        CancellationToken cancellationToken)
    {
        if (!CanAccess(recipientType, recipientId))
        {
            return Forbid();
        }

        var notifications = unitOfWork.Notifications.Query()
            .Where(n => n.RecipientType == recipientType && n.RecipientId == recipientId)
            .ToList();

        foreach (var notification in notifications)
        {
            unitOfWork.Notifications.Delete(notification);
        }

        if (notifications.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    private bool CanAccess(ActorType recipientType, int recipientId)
    {
        if (recipientType == ActorType.Administrator && recipientId == 0)
        {
            return User.IsAdministrator();
        }

        return User.IsAdministrator()
            || recipientType == ActorType.HospitalityPartner && User.HospitalityPartnerId() == recipientId
            || recipientType == ActorType.CharityOrganization && User.CharityOrganizationId() == recipientId
            || recipientType == ActorType.Driver && User.DriverId() == recipientId;
    }
}
