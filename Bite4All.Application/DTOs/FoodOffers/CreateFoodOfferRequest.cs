using Bite4All.Domain.Enums;

namespace Bite4All.Application.DTOs.FoodOffers;

public class CreateFoodOfferRequest
{
    public int HospitalityPartnerId { get; set; }
    public int? SpecialCampaignId { get; set; }
    public decimal TotalQuantityKg { get; set; }
    public FoodCategory Category { get; set; }
    public DateTime PickupWindowStartUtc { get; set; }
    public DateTime PickupWindowEndUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public int MatchResponseWindowMinutes { get; set; } = 30;
    public string? Note { get; set; }
    public string? PhotoUrl { get; set; }
    public List<CreateFoodOfferItemRequest> Items { get; set; } = [];
}

public class CreateFoodOfferItemRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "kom";
    public DietaryTag DietaryTags { get; set; } = DietaryTag.None;
}
