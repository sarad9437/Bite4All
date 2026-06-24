using Bite4All.Application.DTOs.Reports;
using Bite4All.API.Authorization;
using Bite4All.Application.DTOs.Common;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bite4All.API.Controllers;

[ApiController]
[Authorize]
[Route("reputation")]
public class ReputationController(IUnitOfWork unitOfWork) : ControllerBase
{
    [HttpPost("ratings")]
    public async Task<ActionResult<ReputationEntry>> Rate(CreateRatingRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsAdministrator())
        {
            var reviewerMatchesUser =
                request.ReviewerActorType == ActorType.HospitalityPartner && User.HospitalityPartnerId() == request.ReviewerActorId ||
                request.ReviewerActorType == ActorType.CharityOrganization && User.CharityOrganizationId() == request.ReviewerActorId ||
                request.ReviewerActorType == ActorType.Driver && User.DriverId() == request.ReviewerActorId;

            if (!reviewerMatchesUser)
            {
                return Forbid();
            }
        }

        if (request.Rating is < 1 or > 5)
        {
            return BadRequest(new { message = "Rating must be between 1 and 5." });
        }

        if (request.ReviewerActorType == request.RatedActorType && request.ReviewerActorId == request.RatedActorId)
        {
            return BadRequest(new { message = "An actor cannot rate themselves." });
        }

        var pickup = await unitOfWork.PickupDocuments.GetByIdAsync(request.PickupDocumentId, cancellationToken);
        if (pickup is null)
        {
            return NotFound();
        }

        if (pickup.Status is not (PickupStatus.PickedUp or PickupStatus.DeliveredToOrganization))
        {
            return BadRequest(new { message = "Ratings can only be submitted after the pickup is completed." });
        }

        var ratedActorBelongsToPickup =
            request.RatedActorType == ActorType.HospitalityPartner && pickup.HospitalityPartnerId == request.RatedActorId ||
            request.RatedActorType == ActorType.CharityOrganization && pickup.CharityOrganizationId == request.RatedActorId ||
            request.RatedActorType == ActorType.Driver && pickup.DriverId == request.RatedActorId;

        var reviewerBelongsToPickup =
            request.ReviewerActorType == ActorType.HospitalityPartner && pickup.HospitalityPartnerId == request.ReviewerActorId ||
            request.ReviewerActorType == ActorType.CharityOrganization && pickup.CharityOrganizationId == request.ReviewerActorId ||
            request.ReviewerActorType == ActorType.Driver && pickup.DriverId == request.ReviewerActorId;

        if (!ratedActorBelongsToPickup || !reviewerBelongsToPickup)
        {
            return BadRequest(new { message = "Ratings must be tied to actors that participated in the pickup." });
        }

        var alreadyRated = unitOfWork.ReputationEntries.Query().Any(r =>
            r.PickupDocumentId == request.PickupDocumentId &&
            r.ReviewerActorType == request.ReviewerActorType &&
            r.ReviewerActorId == request.ReviewerActorId &&
            r.RatedActorType == request.RatedActorType &&
            r.RatedActorId == request.RatedActorId);

        if (alreadyRated)
        {
            return BadRequest(new { message = "This actor already rated the selected pickup." });
        }

        var rating = new ReputationEntry
        {
            PickupDocumentId = request.PickupDocumentId,
            RatedActorType = request.RatedActorType,
            RatedActorId = request.RatedActorId,
            ReviewerActorType = request.ReviewerActorType,
            ReviewerActorId = request.ReviewerActorId,
            Rating = request.Rating,
            Comment = request.Comment
        };

        await unitOfWork.ReputationEntries.AddAsync(rating, cancellationToken);

        // Fix: recalculate score first so that RecordSnapshotAsync captures the
        // NEW score, not the pre-rating value. Previously the snapshot was recorded
        // before SaveChanges propagated the recalculated score, meaning it always
        // contained the old score. A single SaveChanges at the end is enough.
        await RecalculateScoreAsync(request.RatedActorType, request.RatedActorId, cancellationToken);
        await RecordSnapshotAsync(request.RatedActorType, request.RatedActorId, "Rating", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(rating);
    }

    [Authorize(Roles = "Administrator")]
    [HttpPost("badges/refresh")]
    public async Task<IActionResult> RefreshBadges(CancellationToken cancellationToken)
    {
        // Hospitality partners
        var partners = unitOfWork.HospitalityPartners.Query().ToList();
        foreach (var partner in partners.Where(p => p.SuccessfulDonations >= 50))
        {
            var level = partner.SuccessfulDonations >= 500 && partner.TotalDonatedKg >= 2000 && partner.ReputationScore >= 4.5
                ? BadgeLevel.Gold
                : partner.SuccessfulDonations >= 200 && partner.TotalDonatedKg >= 500
                    ? BadgeLevel.Silver
                    : BadgeLevel.Bronze;

            var alreadyAssigned = unitOfWork.BadgeAssignments.Query().Any(b =>
                b.ActorType == ActorType.HospitalityPartner &&
                b.ActorId == partner.Id &&
                b.Level == level &&
                !b.AssignedByAdmin);

            if (alreadyAssigned) continue;

            await unitOfWork.BadgeAssignments.AddAsync(new BadgeAssignment
            {
                ActorType = ActorType.HospitalityPartner,
                ActorId = partner.Id,
                Level = level,
                Name = $"{level} donor"
            }, cancellationToken);
        }

        // Charity organizations
        var organizations = unitOfWork.CharityOrganizations.Query().ToList();
        foreach (var organization in organizations.Where(o => o.AcceptedMatchCount >= 50))
        {
            var level = organization.AcceptedMatchCount >= 500 && organization.ReputationScore >= 4.5
                ? BadgeLevel.Gold
                : organization.AcceptedMatchCount >= 200
                    ? BadgeLevel.Silver
                    : BadgeLevel.Bronze;

            var alreadyAssigned = unitOfWork.BadgeAssignments.Query().Any(b =>
                b.ActorType == ActorType.CharityOrganization &&
                b.ActorId == organization.Id &&
                b.Level == level &&
                !b.AssignedByAdmin);

            if (alreadyAssigned) continue;

            await unitOfWork.BadgeAssignments.AddAsync(new BadgeAssignment
            {
                ActorType = ActorType.CharityOrganization,
                ActorId = organization.Id,
                Level = level,
                Name = $"{level} recipient"
            }, cancellationToken);
        }

        // Drivers
        var drivers = unitOfWork.Drivers.Query().ToList();
        foreach (var driver in drivers.Where(d => d.CompletedPickups >= 50))
        {
            var level = driver.CompletedPickups >= 500 && driver.ReputationScore >= 4.5
                ? BadgeLevel.Gold
                : driver.CompletedPickups >= 200
                    ? BadgeLevel.Silver
                    : BadgeLevel.Bronze;

            var alreadyAssigned = unitOfWork.BadgeAssignments.Query().Any(b =>
                b.ActorType == ActorType.Driver &&
                b.ActorId == driver.Id &&
                b.Level == level &&
                !b.AssignedByAdmin);

            if (alreadyAssigned) continue;

            await unitOfWork.BadgeAssignments.AddAsync(new BadgeAssignment
            {
                ActorType = ActorType.Driver,
                ActorId = driver.Id,
                Level = level,
                Name = $"{level} driver"
            }, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "Administrator")]
    [HttpPost("badges")]
    public async Task<ActionResult<BadgeAssignment>> AssignBadge(CreateBadgeRequest request, CancellationToken cancellationToken)
    {
        if (request.ActorId <= 0 || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Actor and badge name are required." });
        }

        var duplicate = unitOfWork.BadgeAssignments.Query().Any(b =>
            b.ActorType == request.ActorType &&
            b.ActorId == request.ActorId &&
            b.Level == request.Level &&
            b.Name == request.Name &&
            b.AssignedByAdmin);

        if (duplicate)
        {
            return BadRequest(new { message = "This badge was already assigned." });
        }

        var badge = new BadgeAssignment
        {
            ActorType = request.ActorType,
            ActorId = request.ActorId,
            Level = request.Level,
            Name = request.Name,
            AssignedByAdmin = true
        };

        await unitOfWork.BadgeAssignments.AddAsync(badge, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(badge);
    }

    [Authorize]
    [HttpGet("actors/{actorType}/{actorId:int}")]
    public async Task<ActionResult<ActorReputationDetailsDto>> GetActorReputation(ActorType actorType, int actorId, CancellationToken cancellationToken)
    {
        var details = actorType switch
        {
            ActorType.HospitalityPartner => await GetHospitalityPartnerReputationAsync(actorId, cancellationToken),
            ActorType.CharityOrganization => await GetCharityOrganizationReputationAsync(actorId, cancellationToken),
            ActorType.Driver => await GetDriverReputationAsync(actorId, cancellationToken),
            _ => new ActorReputationDetailsDto { ActorType = actorType, ActorId = actorId }
        };

        return details is null ? NotFound() : Ok(details);
    }

    // ----------------------------------------------------------------
    // Private helpers
    // ----------------------------------------------------------------

    private async Task RecalculateScoreAsync(ActorType actorType, int actorId, CancellationToken cancellationToken)
    {
        var ratings = unitOfWork.ReputationEntries.Query()
            .Where(r => r.RatedActorType == actorType && r.RatedActorId == actorId)
            .Select(r => (double)r.Rating)
            .ToList();
        var average = ratings.Count == 0 ? 3.5 : ratings.Average();

        if (actorType == ActorType.HospitalityPartner)
        {
            var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(actorId, cancellationToken);
            if (partner is not null)
            {
                partner.ReputationScore = CalculateReputationScore(average, partner.SuccessfulDonations, partner.CancellationCount);
            }
        }
        else if (actorType == ActorType.CharityOrganization)
        {
            var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(actorId, cancellationToken);
            if (organization is not null)
            {
                organization.ReputationScore = CalculateReputationScore(average, organization.AcceptedMatchCount, organization.CancellationCount);
            }
        }
        else if (actorType == ActorType.Driver)
        {
            var driver = await unitOfWork.Drivers.GetByIdAsync(actorId, cancellationToken);
            if (driver is not null)
            {
                driver.ReputationScore = CalculateReputationScore(average, driver.CompletedPickups, driver.CancellationCount);
            }
        }
    }

    private static double CalculateReputationScore(double averageRating, int confirmations, int cancellations)
    {
        // Spec: 40% average rating, 30% confirmation rate, 30% cancellation rate (negative impact)
        // All components normalized to 1-5 scale
        var totalOpportunities = Math.Max(1, confirmations + cancellations);
        var confirmationRate = confirmations / (double)totalOpportunities;
        var cancellationRate = cancellations / (double)totalOpportunities;
        
        // Rating component: 40% of final score (averageRating is already 1-5)
        var ratingComponent = averageRating * 0.4;
        
        // Confirmation component: 30% of final score (rate 0-1 scaled to 0-5)
        var confirmationComponent = confirmationRate * 5 * 0.3;
        
        // Cancellation component: 30% of final score (inverted rate 0-1 scaled to 0-5)
        var cancellationComponent = (1 - cancellationRate) * 5 * 0.3;
        
        var score = ratingComponent + confirmationComponent + cancellationComponent;
        return Math.Round(Math.Clamp(score, 1, 5), 2);
    }

    /// <summary>
    /// Records a reputation snapshot capturing the actor's score AFTER
    /// RecalculateScoreAsync has already updated it in the EF change tracker.
    /// Because SaveChanges has not yet been called, we read the score directly
    /// from the in-memory tracked entity rather than re-querying the database,
    /// which would return the stale persisted value.
    /// </summary>
    private async Task RecordSnapshotAsync(ActorType actorType, int actorId, string source, CancellationToken cancellationToken)
    {
        double? score = actorType switch
        {
            ActorType.HospitalityPartner =>
                (await unitOfWork.HospitalityPartners.GetByIdAsync(actorId, cancellationToken))?.ReputationScore,
            ActorType.CharityOrganization =>
                (await unitOfWork.CharityOrganizations.GetByIdAsync(actorId, cancellationToken))?.ReputationScore,
            ActorType.Driver =>
                (await unitOfWork.Drivers.GetByIdAsync(actorId, cancellationToken))?.ReputationScore,
            _ => null
        };

        if (!score.HasValue) return;

        await unitOfWork.ReputationSnapshots.AddAsync(new ReputationSnapshot
        {
            ActorType = actorType,
            ActorId = actorId,
            Score = score.Value,
            Source = source
        }, cancellationToken);
    }

    private async Task<ActorReputationDetailsDto?> GetHospitalityPartnerReputationAsync(int actorId, CancellationToken cancellationToken)
    {
        var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(actorId, cancellationToken);
        if (partner is null) return null;

        var badges = unitOfWork.BadgeAssignments.Query()
            .Where(b => b.ActorType == ActorType.HospitalityPartner && b.ActorId == actorId)
            .Select(b => new BadgeSummaryDto { Name = b.Name, Level = b.Level, AssignedByAdmin = b.AssignedByAdmin })
            .ToList();

        return new ActorReputationDetailsDto
        {
            ActorType = ActorType.HospitalityPartner,
            ActorId = actorId,
            ReputationScore = partner.ReputationScore,
            SuccessfulDonations = partner.SuccessfulDonations,
            CancellationCount = partner.CancellationCount,
            Badges = badges,
            History = unitOfWork.ReputationSnapshots.Query()
                .Where(r => r.ActorType == ActorType.HospitalityPartner && r.ActorId == actorId)
                .OrderBy(r => r.CreatedAtUtc)
                .Select(r => new ReputationHistoryDto { RecordedAtUtc = r.CreatedAtUtc, Score = r.Score, Source = r.Source })
                .ToList()
        };
    }

    private async Task<ActorReputationDetailsDto?> GetCharityOrganizationReputationAsync(int actorId, CancellationToken cancellationToken)
    {
        var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(actorId, cancellationToken);
        if (organization is null) return null;

        var badges = unitOfWork.BadgeAssignments.Query()
            .Where(b => b.ActorType == ActorType.CharityOrganization && b.ActorId == actorId)
            .Select(b => new BadgeSummaryDto { Name = b.Name, Level = b.Level, AssignedByAdmin = b.AssignedByAdmin })
            .ToList();

        return new ActorReputationDetailsDto
        {
            ActorType = ActorType.CharityOrganization,
            ActorId = actorId,
            ReputationScore = organization.ReputationScore,
            SuccessfulDonations = organization.AcceptedMatchCount,
            CancellationCount = organization.CancellationCount,
            Badges = badges,
            History = unitOfWork.ReputationSnapshots.Query()
                .Where(r => r.ActorType == ActorType.CharityOrganization && r.ActorId == actorId)
                .OrderBy(r => r.CreatedAtUtc)
                .Select(r => new ReputationHistoryDto { RecordedAtUtc = r.CreatedAtUtc, Score = r.Score, Source = r.Source })
                .ToList()
        };
    }

    private async Task<ActorReputationDetailsDto?> GetDriverReputationAsync(int actorId, CancellationToken cancellationToken)
    {
        var driver = await unitOfWork.Drivers.GetByIdAsync(actorId, cancellationToken);
        if (driver is null) return null;

        var badges = unitOfWork.BadgeAssignments.Query()
            .Where(b => b.ActorType == ActorType.Driver && b.ActorId == actorId)
            .Select(b => new BadgeSummaryDto { Name = b.Name, Level = b.Level, AssignedByAdmin = b.AssignedByAdmin })
            .ToList();

        return new ActorReputationDetailsDto
        {
            ActorType = ActorType.Driver,
            ActorId = actorId,
            ReputationScore = driver.ReputationScore,
            SuccessfulDonations = driver.CompletedPickups,
            CancellationCount = driver.CancellationCount,
            Badges = badges,
            History = unitOfWork.ReputationSnapshots.Query()
                .Where(r => r.ActorType == ActorType.Driver && r.ActorId == actorId)
                .OrderBy(r => r.CreatedAtUtc)
                .Select(r => new ReputationHistoryDto { RecordedAtUtc = r.CreatedAtUtc, Score = r.Score, Source = r.Source })
                .ToList()
        };
    }
}
