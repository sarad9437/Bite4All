using Bite4All.Domain.Common;
using Bite4All.Domain.Enums;

namespace Bite4All.Domain.Entities;

public class FoodOffer : Entity
{
    public int HospitalityPartnerId { get; set; }
    public HospitalityPartner? HospitalityPartner { get; set; }
    public int? SpecialCampaignId { get; set; }
    public SpecialCampaign? SpecialCampaign { get; set; }
    // Fix: track which RecurrentDonation produced this offer so the scheduler can check
    // per-recurrent-donation instead of per-partner (a partner may have multiple schedules).
    public int? RecurrentDonationId { get; set; }
    public RecurrentDonation? RecurrentDonation { get; set; }
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
