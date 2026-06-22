using Bite4All.Domain.Common;

namespace Bite4All.Domain.Entities;

public class IdempotencyRecord : Entity
{
    public string RouteKey { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public int? StatusCode { get; set; }
    public string? ResponseContentType { get; set; }
    public string? ResponseBody { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
}
