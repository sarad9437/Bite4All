using Bite4All.Domain.Common;
using Bite4All.Domain.Enums;

namespace Bite4All.Domain.Entities;

public class BadgeAssignment : Entity
{
    public ActorType ActorType { get; set; }
    public int ActorId { get; set; }
    public BadgeLevel Level { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool AssignedByAdmin { get; set; }
}
