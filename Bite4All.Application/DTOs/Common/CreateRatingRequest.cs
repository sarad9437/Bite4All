using Bite4All.Domain.Enums;

namespace Bite4All.Application.DTOs.Common;

public class CreateRatingRequest
{
    public int PickupDocumentId { get; set; }
    public ActorType RatedActorType { get; set; }
    public int RatedActorId { get; set; }
    public ActorType ReviewerActorType { get; set; }
    public int ReviewerActorId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
}
