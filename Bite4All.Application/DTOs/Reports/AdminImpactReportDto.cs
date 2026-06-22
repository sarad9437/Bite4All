using System.Text.Json.Serialization;

namespace Bite4All.Application.DTOs.Reports;

public class AdminImpactReportDto
{
    public decimal TotalRescuedKg { get; set; }
    public decimal EstimatedCo2SavedKg { get; set; }
    public int MealsProvided { get; set; }
    public int UnusedOffersCount { get; set; }
    public decimal UnusedOffersKg { get; set; }
    public int UniqueRecipientsHelped { get; set; }
    public int ActiveHospitalityPartners { get; set; }
    public int ActiveOrganizations { get; set; }
    public List<ImpactByCityDto> ByCity { get; set; } = [];
    public List<TopActorImpactDto> TopHospitalityPartners { get; set; } = [];
    public List<TopActorImpactDto> TopOrganizations { get; set; } = [];
    public List<MonthlyDonationTrendDto> MonthlyTrends { get; set; } = [];
    public List<ActorsByCityDto> ActiveActorsByCity { get; set; } = [];
}

public class ImpactByCityDto
{
    public string CityName { get; set; } = string.Empty;
    public decimal RescuedKg { get; set; }
}

public class TopActorImpactDto
{
    public int ActorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal QuantityKg { get; set; }
    public int CompletedPickups { get; set; }
}

public class MonthlyDonationTrendDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal RescuedKg { get; set; }
    public int CompletedPickups { get; set; }
}

public class ActorsByCityDto
{
    public string CityName { get; set; } = string.Empty;
    public int HospitalityPartners { get; set; }
    public int Organizations { get; set; }
}

public class PartnerImpactReportDto
{
    public int HospitalityPartnerId { get; set; }
    public decimal TotalDonatedKg { get; set; }
    public int SuccessfulDonations { get; set; }
    public int UnusedOffersCount { get; set; }
    public decimal UnusedOffersKg { get; set; }
    public double ReputationScore { get; set; }
    public decimal EstimatedCo2SavedKg { get; set; }
    public int MealsProvided { get; set; }
    public string NextBadgeHint { get; set; } = string.Empty;
}

public class OrganizationImpactReportDto
{
    public int CharityOrganizationId { get; set; }
    public decimal TotalReceivedKg { get; set; }
    public int CompletedPickups { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MealsDistributedEstimate { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RecordedMealsDistributed { get; set; }
    public int UniqueRecipientsHelped { get; set; }
    public double ReputationScore { get; set; }
    public string NextBadgeHint { get; set; } = string.Empty;
    public List<ImpactByCategoryDto> ByCategory { get; set; } = [];
    public List<DriverActivityDto> DriverActivity { get; set; } = [];
}

public class ImpactByCategoryDto
{
    public string Category { get; set; } = string.Empty;
    public decimal QuantityKg { get; set; }
}

public class DriverActivityDto
{
    public int? DriverId { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public int CompletedPickups { get; set; }
    public decimal TransportedKg { get; set; }
}
