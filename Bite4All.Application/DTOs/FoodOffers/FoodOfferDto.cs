using Bite4All.Domain.Enums;

namespace Bite4All.Application.DTOs.FoodOffers;

/// <summary>
/// DTO returned for food offer queries.
/// MatchResponseWindowMinutes is included so the client can display a countdown
/// to the organization showing how long they have to respond.
/// </summary>
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
    /// <summary>
    /// How many minutes the top-ranked organization has to accept or decline
    /// before the offer cascades to the next in line.
    /// </summary>
    public int MatchResponseWindowMinutes { get; set; }
    public string? Note { get; set; }
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
