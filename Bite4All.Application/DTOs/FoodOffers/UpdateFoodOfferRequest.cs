using Bite4All.Domain.Enums;

namespace Bite4All.Application.DTOs.FoodOffers;

public class UpdateFoodOfferRequest
{
    public decimal? TotalQuantityKg { get; set; }
    public FoodCategory? Category { get; set; }
    public DateTime? PickupWindowStartUtc { get; set; }
    public DateTime? PickupWindowEndUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public int? MatchResponseWindowMinutes { get; set; }
    public string? Note { get; set; }
    public string? PhotoUrl { get; set; }
}
