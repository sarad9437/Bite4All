using Bite4All.Domain.Enums;

namespace Bite4All.Application.DTOs.Common;

public class CreateBadgeRequest
{
    public ActorType ActorType { get; set; }
    public int ActorId { get; set; }
    public BadgeLevel Level { get; set; }
    public string Name { get; set; } = string.Empty;
}
