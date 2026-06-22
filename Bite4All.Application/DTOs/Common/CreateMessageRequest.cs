using Bite4All.Domain.Enums;

namespace Bite4All.Application.DTOs.Common;

public class CreateMessageRequest
{
    public ActorType SenderType { get; set; }
    public int? SenderId { get; set; }
    public ActorType RecipientType { get; set; }
    public int RecipientId { get; set; }
    public int? PickupDocumentId { get; set; }
    public int? FoodOfferId { get; set; }
    public string Body { get; set; } = string.Empty;
}
