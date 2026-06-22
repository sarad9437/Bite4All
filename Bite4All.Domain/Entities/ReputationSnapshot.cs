using Bite4All.Domain.Common;
using Bite4All.Domain.Enums;

namespace Bite4All.Domain.Entities;

public class ReputationSnapshot : Entity
{
    public ActorType ActorType { get; set; }
    public int ActorId { get; set; }
    public double Score { get; set; }
    public string Source { get; set; } = string.Empty;
}
