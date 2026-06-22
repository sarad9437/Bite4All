using Bite4All.Domain.Enums;

namespace Bite4All.Application.DTOs.FoodOffers;

public class FoodOfferDto
{
    public int Id { get; set; }
    public string PartnerName { get; set; } = string.Empty;
    public decimal TotalQuantityKg { get; set; }
    public FoodCategory Category { get; set; }
    public FoodOfferStatus Status { get; set; }
    public DateTime PickupWindowStartUtc { get; set; }
    public DateTime PickupWindowEndUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string? PhotoUrl { get; set; }
    public List<FoodOfferItemDto> Items { get; set; } = [];
}

public class FoodOfferItemDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DietaryTag DietaryTags { get; set; }
}
