using Bite4All.Domain.Common;
using Bite4All.Domain.Enums;

namespace Bite4All.Domain.Entities;

public class PlatformMessage : Entity
{
    public ActorType SenderType { get; set; }
    public int? SenderId { get; set; }
    public ActorType RecipientType { get; set; }
    public int RecipientId { get; set; }
    public int? PickupDocumentId { get; set; }
    public PickupDocument? PickupDocument { get; set; }
    public int? FoodOfferId { get; set; }
    public FoodOffer? FoodOffer { get; set; }
    public MessageType MessageType { get; set; } = MessageType.User;
    public string Body { get; set; } = string.Empty;
}
