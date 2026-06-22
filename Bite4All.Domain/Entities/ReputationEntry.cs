using Bite4All.Domain.Common;
using Bite4All.Domain.Enums;

namespace Bite4All.Domain.Entities;

public class ReputationEntry : Entity
{
    public int PickupDocumentId { get; set; }
    public PickupDocument? PickupDocument { get; set; }
    public ActorType RatedActorType { get; set; }
    public int RatedActorId { get; set; }
    public ActorType ReviewerActorType { get; set; }
    public int ReviewerActorId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
}
