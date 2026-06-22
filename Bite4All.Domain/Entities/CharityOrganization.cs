using Bite4All.Domain.Common;
using Bite4All.Domain.Enums;

namespace Bite4All.Domain.Entities;

public class CharityOrganization : Entity
{
    public string Name { get; set; } = string.Empty;
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
    public decimal DailyCapacityKg { get; set; }
    public decimal TemporaryExtraCapacityKg { get; set; }
    public DateTime? TemporaryCapacityExpiresAtUtc { get; set; }
    public double ReputationScore { get; set; } = 3.5;
    public DateTime? LastReceivedAtUtc { get; set; }
    public decimal MatchCompensationBonus { get; set; }
    public DateTime? MatchCompensationExpiresAtUtc { get; set; }
    public int AcceptedMatchCount { get; set; }
    public int CancellationCount { get; set; }
    public List<Driver> Drivers { get; set; } = [];
    public List<Vehicle> Vehicles { get; set; } = [];
    public List<Recipient> Recipients { get; set; } = [];
}
