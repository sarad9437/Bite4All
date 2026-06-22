using Bite4All.Domain.Enums;

namespace Bite4All.Application.DTOs.FoodOffers;

public class FoodOfferSearchRequest
{
    public string? PartnerName { get; set; }
    public int? CityId { get; set; }
    public FoodCategory? Category { get; set; }
    public FoodOfferStatus? Status { get; set; }
    public decimal? MinQuantityKg { get; set; }
    public decimal? MaxQuantityKg { get; set; }
    public DateTime? PickupFromUtc { get; set; }
    public DateTime? PickupToUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
