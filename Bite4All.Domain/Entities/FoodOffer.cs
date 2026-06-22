using Bite4All.Domain.Common;
using Bite4All.Domain.Enums;

namespace Bite4All.Domain.Entities;

public class FoodOffer : Entity
{
    public int HospitalityPartnerId { get; set; }
    public HospitalityPartner? HospitalityPartner { get; set; }
    public int? SpecialCampaignId { get; set; }
    public SpecialCampaign? SpecialCampaign { get; set; }
    public decimal TotalQuantityKg { get; set; }
    public FoodCategory Category { get; set; }
    public DateTime PickupWindowStartUtc { get; set; }
    public DateTime PickupWindowEndUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public int MatchResponseWindowMinutes { get; set; } = 30;
    public string? Note { get; set; }
    public string? PhotoUrl { get; set; }
    public FoodOfferStatus Status { get; set; } = FoodOfferStatus.Active;
    public bool CreatedFromRecurrentDonation { get; set; }
    public List<FoodOfferItem> Items { get; set; } = [];
    public List<OfferMatch> Matches { get; set; } = [];
}
