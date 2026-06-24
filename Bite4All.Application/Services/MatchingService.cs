using Bite4All.Application.DTOs.Matching;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Domain.Repositories;

namespace Bite4All.Application.Services;

public class MatchingService(IUnitOfWork unitOfWork) : IMatchingService
{
    public async Task<List<MatchCandidateDto>> GenerateMatchesAsync(int foodOfferId, CancellationToken cancellationToken = default)
    {
        var offer = unitOfWork.FoodOffers.Query().FirstOrDefault(o => o.Id == foodOfferId);

        if (offer?.HospitalityPartner is null)
        {
            var partner = offer is null ? null : await unitOfWork.HospitalityPartners.GetByIdAsync(offer.HospitalityPartnerId, cancellationToken);
            if (offer is null || partner is null)
            {
                return [];
            }

            offer.HospitalityPartner = partner;
        }

        if (offer.Items.Count == 0)
        {
            offer.Items = unitOfWork.FoodOfferItems.Query().Where(i => i.FoodOfferId == offer.Id).ToList();
        }

        if (offer.HospitalityPartner is null)
        {
            return [];
        }

        var existingMatches = unitOfWork.OfferMatches.Query()
            .Where(m => m.FoodOfferId == foodOfferId)
            .OrderBy(m => m.Rank)
            .ToList();

        var hasPendingOrAccepted = existingMatches.Any(m =>
            m.Decision == MatchDecision.Pending || m.Decision == MatchDecision.Accepted);

        if (existingMatches.Count > 0 && hasPendingOrAccepted)
        {
            var organizationIds = existingMatches.Select(m => m.CharityOrganizationId).Distinct().ToList();
            var organizationLookup = unitOfWork.CharityOrganizations.Query()
                .Where(o => organizationIds.Contains(o.Id))
                .ToDictionary(o => o.Id, o => o.Name);

            return existingMatches
                .Where(m => m.Rank > 0)
                .Select(m => new MatchCandidateDto
                {
                    OrganizationId = m.CharityOrganizationId,
                    OrganizationName = organizationLookup.GetValueOrDefault(m.CharityOrganizationId, string.Empty),
                    Score = m.Score,
                    Rank = m.Rank,
                    Decision = m.Decision
                }).ToList();
        }

        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        // Fix 1: capacity filter must account for pickups picked up today (not just created today).
        // A pickup created yesterday but picked up today still consumes today's capacity.
        // Active (in-progress) pickups created today that are not yet completed also reserve capacity.
        var usedCapacity = unitOfWork.PickupDocuments.Query()
            .Where(p => p.Status != PickupStatus.Cancelled &&
                        (
                            // In-progress pickups created today (not yet picked up) — they reserve capacity
                            (p.CreatedAtUtc >= todayUtc && p.CreatedAtUtc < tomorrowUtc &&
                             p.Status != PickupStatus.DeliveredToOrganization &&
                             p.Status != PickupStatus.PickedUp)
                            ||
                            // Pickups actually picked up today (may have been created on a previous day)
                            (p.PickedUpAtUtc.HasValue &&
                             p.PickedUpAtUtc.Value >= todayUtc &&
                             p.PickedUpAtUtc.Value < tomorrowUtc)
                        ))
            .GroupBy(p => p.CharityOrganizationId)
            .Select(g => new { OrganizationId = g.Key, TotalKg = g.Sum(p => p.ActualQuantityKg ?? p.PlannedQuantityKg) })
            .ToDictionary(x => x.OrganizationId, x => x.TotalKg);

        var blockedOrganizationIds = unitOfWork.BlockRelations.Query()
            .Where(b => b.IsActive &&
                        b.HospitalityPartnerId == offer.HospitalityPartnerId &&
                        (b.BlockedByHospitalityPartner || b.BlockedByOrganization))
            .Select(b => b.CharityOrganizationId)
            .ToList();

        var organizations = unitOfWork.CharityOrganizations.Query()
            .Where(o => o.ApprovalStatus == ApprovalStatus.Approved && o.CityId == offer.HospitalityPartner.CityId)
            .ToList();

        var candidateOrganizationIds = organizations.Select(o => o.Id).ToList();
        var badgeLevels = unitOfWork.BadgeAssignments.Query()
            .Where(b => b.ActorType == ActorType.CharityOrganization && candidateOrganizationIds.Contains(b.ActorId))
            .GroupBy(b => b.ActorId)
            .ToDictionary(g => g.Key, g => g.Max(b => b.Level));

        foreach (var organization in organizations.Where(o => o.Recipients.Count == 0))
        {
            organization.Recipients = unitOfWork.Recipients.Query()
                .Where(r => r.CharityOrganizationId == organization.Id && r.IsActive)
                .ToList();
        }

        var candidates = new List<(CharityOrganization Organization, decimal Score)>();

        foreach (var organization in organizations)
        {
            if (blockedOrganizationIds.Contains(organization.Id))
            {
                await AddSkippedMatchAsync(offer.Id, organization.Id, MatchDecision.SkippedByBlock, cancellationToken);
                continue;
            }

            var temporaryExtra = organization.TemporaryCapacityExpiresAtUtc.HasValue
                                 && organization.TemporaryCapacityExpiresAtUtc > DateTime.UtcNow
                ? organization.TemporaryExtraCapacityKg
                : 0m;
            var remainingCapacity = organization.DailyCapacityKg + temporaryExtra -
                                    usedCapacity.GetValueOrDefault(organization.Id);
            if (remainingCapacity < offer.TotalQuantityKg)
            {
                await AddSkippedMatchAsync(offer.Id, organization.Id, MatchDecision.SkippedByCapacity, cancellationToken);
                continue;
            }

            if (!IsOfferCompatibleWithRecipients(offer.Items, organization.Recipients))
            {
                await AddSkippedMatchAsync(offer.Id, organization.Id, MatchDecision.SkippedByDiet, cancellationToken);
                continue;
            }

            var distanceScore = CalculateDistanceScore(offer.HospitalityPartner, organization);
            var dietScore = offer.Items.All(i => i.DietaryTags == DietaryTag.None) ? 10 : 18;
            var capacityScore = Math.Min(20, remainingCapacity);
            var rotationScore = organization.LastReceivedAtUtc is null
                ? 20
                : Math.Min(20, (decimal)(DateTime.UtcNow - organization.LastReceivedAtUtc.Value).TotalDays);
            var reputationScore = (decimal)organization.ReputationScore * 4;
            var cancellationPenalty = organization.CancellationCount * 2;
            var compensationBonus = GetCompensationBonus(organization);
            var badgeBonus = GetBadgeBonus(badgeLevels.GetValueOrDefault(organization.Id));

            candidates.Add((organization, distanceScore + dietScore + capacityScore + rotationScore + reputationScore - cancellationPenalty + compensationBonus + badgeBonus));
        }

        var ranked = candidates
            .OrderByDescending(c => c.Score)
            .Select((c, index) => new MatchCandidateDto
            {
                OrganizationId = c.Organization.Id,
                OrganizationName = c.Organization.Name,
                Score = Math.Round(c.Score, 2),
                Rank = index + 1,
                Decision = MatchDecision.Pending
            })
            .ToList();

        foreach (var candidate in ranked)
        {
            await unitOfWork.OfferMatches.AddAsync(new OfferMatch
            {
                FoodOfferId = offer.Id,
                CharityOrganizationId = candidate.OrganizationId,
                Score = candidate.Score,
                Rank = candidate.Rank
            }, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ranked;
    }

    private async Task AddSkippedMatchAsync(int offerId, int organizationId, MatchDecision decision, CancellationToken cancellationToken)
    {
        await unitOfWork.OfferMatches.AddAsync(new OfferMatch
        {
            FoodOfferId = offerId,
            CharityOrganizationId = organizationId,
            Decision = decision,
            Score = 0,
            Rank = 0
        }, cancellationToken);
    }

    /// <summary>
    /// Returns true when the offer is compatible with the organization's recipients.
    ///
    /// Per-spec semantics: "sistem ne šalje hranu sa glutenom organizaciji ako zna da njen
    /// primalac ne sme gluten" - if ANY recipient has a restriction, the organization should
    /// not receive food that violates that restriction.
    ///
    /// Implementation:
    ///   - If an organization has a recipient with restriction X (e.g., gluten-free), reject
    ///     offers that contain ANY items with restriction X (e.g., contains gluten).
    ///   - Recipients with no restrictions accept any item.
    ///   - This ensures organizations don't receive food that would be unsafe for ANY of
    ///     their recipients, while being less restrictive than requiring ALL items to satisfy
    ///     ALL restrictions simultaneously.
    /// </summary>
    private static bool IsOfferCompatibleWithRecipients(
        IReadOnlyCollection<FoodOfferItem> items,
        IReadOnlyCollection<Recipient> activeRecipients)
    {
        if (activeRecipients.Count == 0)
        {
            return true;
        }

        var restrictedRecipients = activeRecipients
            .Where(r => r.DietaryRestrictions != DietaryTag.None)
            .ToList();

        if (restrictedRecipients.Count == 0)
        {
            return true;
        }

        if (items.Count == 0)
        {
            return true;
        }

        // Collect all dietary restrictions across all restricted recipients
        var allRestrictions = DietaryTag.None;
        foreach (var recipient in restrictedRecipients)
        {
            allRestrictions |= recipient.DietaryRestrictions;
        }

        // Check if any item violates any of the organization's restrictions
        // An item violates restrictions if it has a flag that conflicts with recipient restrictions
        // For example: if recipient is gluten-free, an item marked as "contains gluten" would violate
        foreach (var item in items)
        {
            // If the item has dietary tags, check if any of them conflict with recipient restrictions
            // For this implementation, we assume items are marked with what they ARE (e.g., Vegetarian, Vegan)
            // rather than what they contain (e.g., "ContainsGluten"). So if an item doesn't have the
            // required tag for a recipient, it's incompatible.
            foreach (var recipient in restrictedRecipients)
            {
                // If recipient requires specific dietary tags and the item doesn't have them, it's incompatible
                if (recipient.DietaryRestrictions != DietaryTag.None &&
                    (item.DietaryTags & recipient.DietaryRestrictions) != recipient.DietaryRestrictions)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static decimal CalculateDistanceScore(HospitalityPartner partner, CharityOrganization organization)
    {
        var latDelta = Math.Abs(partner.Latitude - organization.Latitude);
        var lonDelta = Math.Abs(partner.Longitude - organization.Longitude);
        var roughDistance = latDelta + lonDelta;
        return Math.Max(0, 25 - roughDistance * 10);
    }

    private static decimal GetBadgeBonus(BadgeLevel? badgeLevel) => badgeLevel switch
    {
        BadgeLevel.Platinum => 4m,
        BadgeLevel.Gold     => 2m,
        BadgeLevel.Silver   => 0.75m,
        BadgeLevel.Bronze   => 0.25m,
        BadgeLevel.Special  => 1m,
        _                   => 0m
    };

    private static decimal GetCompensationBonus(CharityOrganization organization)
    {
        if (organization.MatchCompensationExpiresAtUtc is null || organization.MatchCompensationExpiresAtUtc <= DateTime.UtcNow)
        {
            return 0m;
        }

        return organization.MatchCompensationBonus;
    }
}
