using Bite4All.Application.DTOs.Reports;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Domain.Repositories;

namespace Bite4All.Application.Services;

public class ImpactReportService(IUnitOfWork unitOfWork) : IImpactReportService
{
    public async Task<AdminImpactReportDto> GetAdminImpactAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default)
    {
        var pickups = unitOfWork.PickupDocuments.Query()
            .Where(p => p.Status == PickupStatus.PickedUp || p.Status == PickupStatus.DeliveredToOrganization);
        var unusedOffers = FilterUnusedExpiredOffers(fromUtc, toUtc);

        if (fromUtc.HasValue)
        {
            pickups = pickups.Where(p => p.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            pickups = pickups.Where(p => p.CreatedAtUtc <= toUtc.Value);
        }

        var pickupList = pickups.ToList();
        var unusedOfferList = unusedOffers.ToList();
        foreach (var pickup in pickupList.Where(p => p.HospitalityPartner is null))
        {
            pickup.HospitalityPartner = await unitOfWork.HospitalityPartners.GetByIdAsync(pickup.HospitalityPartnerId, cancellationToken);
            if (pickup.HospitalityPartner is not null && pickup.HospitalityPartner.City is null)
            {
                pickup.HospitalityPartner.City = await unitOfWork.Cities.GetByIdAsync(pickup.HospitalityPartner.CityId, cancellationToken);
            }
        }

        foreach (var pickup in pickupList.Where(p => p.CharityOrganization is null))
        {
            pickup.CharityOrganization = await unitOfWork.CharityOrganizations.GetByIdAsync(pickup.CharityOrganizationId, cancellationToken);
        }

        var totalKg = pickupList.Sum(p => p.ActualQuantityKg ?? p.PlannedQuantityKg);

        var distributionsQuery = unitOfWork.RecipientMealDistributions.Query();
        if (fromUtc.HasValue)
        {
            distributionsQuery = distributionsQuery.Where(d => d.DistributedAtUtc >= fromUtc.Value);
        }
        if (toUtc.HasValue)
        {
            distributionsQuery = distributionsQuery.Where(d => d.DistributedAtUtc <= toUtc.Value);
        }
        var uniqueRecipientsHelped = distributionsQuery.Select(d => d.RecipientId).Distinct().Count();

        return new AdminImpactReportDto
        {
            TotalRescuedKg = totalKg,
            EstimatedCo2SavedKg = totalKg * 2.5m,
            MealsProvided = (int)Math.Floor(totalKg * 2),
            UnusedOffersCount = unusedOfferList.Count,
            UnusedOffersKg = unusedOfferList.Sum(o => o.TotalQuantityKg),
            UniqueRecipientsHelped = uniqueRecipientsHelped,
            ActiveHospitalityPartners = unitOfWork.HospitalityPartners.Query().Count(p => p.ApprovalStatus == ApprovalStatus.Approved),
            ActiveOrganizations = unitOfWork.CharityOrganizations.Query().Count(o => o.ApprovalStatus == ApprovalStatus.Approved),
            ByCity = pickupList
                .GroupBy(p => p.HospitalityPartner?.City?.Name ?? "Nepoznat grad")
                .Select(g => new ImpactByCityDto
                {
                    CityName = g.Key,
                    RescuedKg = g.Sum(p => p.ActualQuantityKg ?? p.PlannedQuantityKg)
                })
                .OrderByDescending(x => x.RescuedKg)
                .ToList(),
            TopHospitalityPartners = pickupList
                .GroupBy(p => new { p.HospitalityPartnerId, Name = p.HospitalityPartner?.Name ?? "Unknown" })
                .Select(g => new TopActorImpactDto
                {
                    ActorId = g.Key.HospitalityPartnerId,
                    Name = g.Key.Name,
                    QuantityKg = g.Sum(p => p.ActualQuantityKg ?? p.PlannedQuantityKg),
                    CompletedPickups = g.Count()
                })
                .OrderByDescending(x => x.QuantityKg)
                .Take(10)
                .ToList(),
            TopOrganizations = pickupList
                .GroupBy(p => new { p.CharityOrganizationId, Name = p.CharityOrganization?.Name ?? "Unknown" })
                .Select(g => new TopActorImpactDto
                {
                    ActorId = g.Key.CharityOrganizationId,
                    Name = g.Key.Name,
                    QuantityKg = g.Sum(p => p.ActualQuantityKg ?? p.PlannedQuantityKg),
                    CompletedPickups = g.Count()
                })
                .OrderByDescending(x => x.QuantityKg)
                .Take(10)
                .ToList(),
            MonthlyTrends = pickupList
                .GroupBy(p => new { p.CreatedAtUtc.Year, p.CreatedAtUtc.Month })
                .Select(g => new MonthlyDonationTrendDto
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    RescuedKg = g.Sum(p => p.ActualQuantityKg ?? p.PlannedQuantityKg),
                    CompletedPickups = g.Count()
                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ToList(),
            ActiveActorsByCity = BuildActiveActorsByCity()
        };
    }

    public async Task<PartnerImpactReportDto?> GetPartnerImpactAsync(int hospitalityPartnerId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default)
    {
        var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(hospitalityPartnerId, cancellationToken);
        if (partner is null)
        {
            return null;
        }

        var pickups = FilterCompletedPickups(fromUtc, toUtc)
            .Where(p => p.HospitalityPartnerId == hospitalityPartnerId)
            .ToList();

        var totalKg = pickups.Sum(p => p.ActualQuantityKg ?? p.PlannedQuantityKg);
        var unusedOffers = FilterUnusedExpiredOffers(fromUtc, toUtc)
            .Where(o => o.HospitalityPartnerId == hospitalityPartnerId)
            .ToList();

        return new PartnerImpactReportDto
        {
            HospitalityPartnerId = hospitalityPartnerId,
            TotalDonatedKg = totalKg,
            SuccessfulDonations = pickups.Count,
            UnusedOffersCount = unusedOffers.Count,
            UnusedOffersKg = unusedOffers.Sum(o => o.TotalQuantityKg),
            ReputationScore = partner.ReputationScore,
            EstimatedCo2SavedKg = totalKg * 2.5m,
            MealsProvided = (int)Math.Floor(totalKg * 2),
            NextBadgeHint = GetNextBadgeHint(partner.SuccessfulDonations, partner.TotalDonatedKg, partner.ReputationScore)
        };
    }

    public async Task<OrganizationImpactReportDto?> GetOrganizationImpactAsync(int charityOrganizationId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default)
    {
        var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(charityOrganizationId, cancellationToken);
        if (organization is null)
        {
            return null;
        }

        var pickups = FilterCompletedPickups(fromUtc, toUtc)
            .Where(p => p.CharityOrganizationId == charityOrganizationId)
            .ToList();

        foreach (var pickup in pickups.Where(p => p.FoodOffer is null))
        {
            pickup.FoodOffer = await unitOfWork.FoodOffers.GetByIdAsync(pickup.FoodOfferId, cancellationToken);
        }

        foreach (var pickup in pickups.Where(p => p.Driver is null && p.DriverId.HasValue))
        {
            pickup.Driver = await unitOfWork.Drivers.GetByIdAsync(pickup.DriverId!.Value, cancellationToken);
        }

        var totalKg = pickups.Sum(p => p.ActualQuantityKg ?? p.PlannedQuantityKg);

        // Fix: apply the same date range to distributions so the organization impact report
        // is internally consistent — when the caller filters by period, meals and recipients
        // must also reflect only that period, not all-time totals.
        var distributionsQuery = unitOfWork.RecipientMealDistributions.Query()
            .Where(d => d.CharityOrganizationId == charityOrganizationId);

        if (fromUtc.HasValue)
        {
            distributionsQuery = distributionsQuery.Where(d => d.DistributedAtUtc >= fromUtc.Value);
        }
        if (toUtc.HasValue)
        {
            distributionsQuery = distributionsQuery.Where(d => d.DistributedAtUtc <= toUtc.Value);
        }

        var distributions = distributionsQuery.ToList();
        var recordedMeals = distributions.Sum(d => d.MealsCount);

        return new OrganizationImpactReportDto
        {
            CharityOrganizationId = charityOrganizationId,
            TotalReceivedKg = totalKg,
            CompletedPickups = pickups.Count,
            MealsDistributedEstimate = recordedMeals > 0 ? null : (int)Math.Floor(totalKg * 2),
            RecordedMealsDistributed = recordedMeals > 0 ? recordedMeals : null,
            UniqueRecipientsHelped = distributions.Select(d => d.RecipientId).Distinct().Count(),
            ReputationScore = organization.ReputationScore,
            NextBadgeHint = GetNextOrganizationBadgeHint(organization.AcceptedMatchCount, organization.ReputationScore),
            ByCategory = pickups
                .Where(p => p.FoodOffer is not null)
                .GroupBy(p => p.FoodOffer!.Category)
                .Select(g => new ImpactByCategoryDto
                {
                    Category = g.Key.ToString(),
                    QuantityKg = g.Sum(p => p.ActualQuantityKg ?? p.PlannedQuantityKg)
                })
                .OrderByDescending(x => x.QuantityKg)
                .ToList(),
            DriverActivity = pickups
                .GroupBy(p => new { p.DriverId, Name = p.Driver?.FullName ?? "Unassigned" })
                .Select(g => new DriverActivityDto
                {
                    DriverId = g.Key.DriverId,
                    DriverName = g.Key.Name,
                    CompletedPickups = g.Count(),
                    TransportedKg = g.Sum(p => p.ActualQuantityKg ?? p.PlannedQuantityKg)
                })
                .OrderByDescending(x => x.TransportedKg)
                .ToList()
        };
    }

    public async Task<DriverImpactReportDto?> GetDriverImpactAsync(int driverId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default)
    {
        var driver = await unitOfWork.Drivers.GetByIdAsync(driverId, cancellationToken);
        if (driver is null)
        {
            return null;
        }

        var pickups = FilterCompletedPickups(fromUtc, toUtc)
            .Where(p => p.DriverId == driverId)
            .ToList();

        var transportedKg = pickups.Sum(p => p.ActualQuantityKg ?? p.PlannedQuantityKg);

        var badges = unitOfWork.BadgeAssignments.Query()
            .Where(b => b.ActorType == ActorType.Driver && b.ActorId == driverId)
            .Select(b => new BadgeSummaryDto { Name = b.Name, Level = b.Level, AssignedByAdmin = b.AssignedByAdmin })
            .ToList();

        var history = unitOfWork.ReputationSnapshots.Query()
            .Where(r => r.ActorType == ActorType.Driver && r.ActorId == driverId)
            .OrderBy(r => r.CreatedAtUtc)
            .Select(r => new ReputationHistoryDto
            {
                RecordedAtUtc = r.CreatedAtUtc,
                Score = r.Score,
                Source = r.Source
            })
            .ToList();

        return new DriverImpactReportDto
        {
            DriverId = driverId,
            DriverName = driver.FullName,
            CompletedPickups = driver.CompletedPickups,
            TransportedKg = transportedKg,
            CancellationCount = driver.CancellationCount,
            ReputationScore = driver.ReputationScore,
            NextBadgeHint = GetNextDriverBadgeHint(driver.CompletedPickups, driver.ReputationScore),
            ReputationHistory = history,
            Badges = badges
        };
    }

    private IQueryable<PickupDocument> FilterCompletedPickups(DateTime? fromUtc, DateTime? toUtc)
    {
        var pickups = unitOfWork.PickupDocuments.Query()
            .Where(p => p.Status == PickupStatus.PickedUp || p.Status == PickupStatus.DeliveredToOrganization);

        if (fromUtc.HasValue)
        {
            pickups = pickups.Where(p => p.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            pickups = pickups.Where(p => p.CreatedAtUtc <= toUtc.Value);
        }

        return pickups;
    }

    private IQueryable<FoodOffer> FilterUnusedExpiredOffers(DateTime? fromUtc, DateTime? toUtc)
    {
        var completedPickupOfferIds = unitOfWork.PickupDocuments.Query()
            .Where(p => p.Status == PickupStatus.PickedUp || p.Status == PickupStatus.DeliveredToOrganization)
            .Select(p => p.FoodOfferId);

        var offers = unitOfWork.FoodOffers.Query()
            .Where(o => o.Status == FoodOfferStatus.Expired && !completedPickupOfferIds.Contains(o.Id));

        if (fromUtc.HasValue)
        {
            offers = offers.Where(o => o.ExpiresAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            offers = offers.Where(o => o.ExpiresAtUtc <= toUtc.Value);
        }

        return offers;
    }

    private static string GetNextOrganizationBadgeHint(int acceptedMatchCount, double reputationScore)
    {
        if (acceptedMatchCount < 50)
        {
            return $"Bronze badge at 50 accepted pickups ({50 - acceptedMatchCount} remaining).";
        }

        if (acceptedMatchCount < 200)
        {
            return "Silver badge at 200 accepted pickups.";
        }

        if (acceptedMatchCount < 500 || reputationScore < 4.5)
        {
            return "Gold badge at 500 accepted pickups and 4.5 reputation.";
        }

        return "Eligible for manual Platinum or special community badge review.";
    }

    private static string GetNextBadgeHint(int successfulDonations, decimal totalDonatedKg, double reputationScore)
    {
        if (successfulDonations < 50)
        {
            return $"Bronze badge at 50 successful donations ({50 - successfulDonations} remaining).";
        }

        if (successfulDonations < 200 || totalDonatedKg < 500)
        {
            return "Silver badge at 200 successful donations and 500kg donated.";
        }

        if (successfulDonations < 500 || totalDonatedKg < 2000 || reputationScore < 4.5)
        {
            return "Gold badge at 500 successful donations, 2000kg donated and 4.5 reputation.";
        }

        return "Eligible for manual Platinum or special community badge review.";
    }

    private static string GetNextDriverBadgeHint(int completedPickups, double reputationScore)
    {
        if (completedPickups < 50)
        {
            return $"Bronze badge at 50 completed pickups ({50 - completedPickups} remaining).";
        }

        if (completedPickups < 200)
        {
            return "Silver badge at 200 completed pickups.";
        }

        if (completedPickups < 500 || reputationScore < 4.5)
        {
            return "Gold badge at 500 completed pickups and 4.5 reputation.";
        }

        return "Eligible for manual Platinum or special community badge review.";
    }

    private List<ActorsByCityDto> BuildActiveActorsByCity()
    {
        var cities = unitOfWork.Cities.Query().ToList();
        var partners = unitOfWork.HospitalityPartners.Query()
            .Where(p => p.ApprovalStatus == ApprovalStatus.Approved)
            .ToList();
        var organizations = unitOfWork.CharityOrganizations.Query()
            .Where(o => o.ApprovalStatus == ApprovalStatus.Approved)
            .ToList();

        return cities
            .Select(city => new ActorsByCityDto
            {
                CityName = city.Name,
                HospitalityPartners = partners.Count(p => p.CityId == city.Id),
                Organizations = organizations.Count(o => o.CityId == city.Id)
            })
            .Where(x => x.HospitalityPartners > 0 || x.Organizations > 0)
            .OrderBy(x => x.CityName)
            .ToList();
    }
}
