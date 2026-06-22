using Bite4All.Domain.Common;
using Bite4All.Domain.Enums;

namespace Bite4All.Domain.Entities;

public class HospitalityPartner : Entity
{
    public string Name { get; set; } = string.Empty;
    public string PartnerType { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int CityId { get; set; }
    public City? City { get; set; }
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string TaxIdentificationNumber { get; set; } = string.Empty;
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
    public string? RejectionReason { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public double ReputationScore { get; set; } = 3.5;
    public int SuccessfulDonations { get; set; }
    public decimal TotalDonatedKg { get; set; }
    public int CancellationCount { get; set; }
    public List<FoodOffer> FoodOffers { get; set; } = [];
}
