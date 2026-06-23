using Bite4All.API.Authorization;
using Bite4All.API.Hubs;
using Bite4All.Application.DTOs.Common;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bite4All.API.Controllers;

[ApiController]
[Authorize(Roles = "CharityOrganization,Administrator")]
[Route("matches")]
public class MatchesController(IUnitOfWork unitOfWork, INotificationPublisher notificationPublisher) : ControllerBase
{
    /// <summary>
    /// Fix 3: returns a paginated list of offer matches for the currently authenticated
    /// charity organization (or any organization when called by an admin).
    /// Supports optional filtering by decision and date range.
    /// Skipped matches (SkippedByBlock, SkippedByCapacity, SkippedByDiet) are excluded
    /// by default unless the caller explicitly asks for them via includeSkipped=true.
    /// </summary>
    [HttpGet("my")]
    public ActionResult<PagedResult<OfferMatch>> GetMine(
        [FromQuery] MatchDecision? decision = null,
        [FromQuery] bool includeSkipped = false,
        [FromQuery] int? organizationId = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        int resolvedOrgId;

        if (User.IsAdministrator())
        {
            if (organizationId is null or <= 0)
            {
                return BadRequest(new { message = "Administrators must supply organizationId." });
            }
            resolvedOrgId = organizationId.Value;
        }
        else
        {
            var callerOrgId = User.CharityOrganizationId();
            if (callerOrgId is null)
            {
                return Forbid();
            }
            resolvedOrgId = callerOrgId.Value;
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var skippedDecisions = new[]
        {
            MatchDecision.SkippedByBlock,
            MatchDecision.SkippedByCapacity,
            MatchDecision.SkippedByDiet
        };

        var query = unitOfWork.OfferMatches.Query()
            .Where(m => m.CharityOrganizationId == resolvedOrgId);

        if (!includeSkipped)
        {
            query = query.Where(m => !skippedDecisions.Contains(m.Decision));
        }

        if (decision.HasValue)
        {
            query = query.Where(m => m.Decision == decision.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(m => m.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(m => m.CreatedAtUtc <= toUtc.Value);
        }

        var totalCount = query.Count();

        var items = query
            .OrderByDescending(m => m.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new PagedResult<OfferMatch>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    [HttpPost("{matchId}/accept")]
    public async Task<IActionResult> Accept(int matchId, CancellationToken cancellationToken)
    {
        var match = unitOfWork.OfferMatches.Query().FirstOrDefault(m => m.Id == matchId);
        if (match is not null)
        {
            match.FoodOffer ??= await unitOfWork.FoodOffers.GetByIdAsync(match.FoodOfferId, cancellationToken);
        }
        if (match?.FoodOffer is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.CharityOrganizationId() != match.CharityOrganizationId)
        {
            return Forbid();
        }

        if (!User.IsAdministrator())
        {
            var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(match.CharityOrganizationId, cancellationToken);
            if (organization is null || organization.ApprovalStatus != ApprovalStatus.Approved)
            {
                return Forbid();
            }
        }

        if (match.Decision != MatchDecision.Pending)
        {
            return BadRequest(new { message = "Only pending matches can be accepted." });
        }

        if (match.FoodOffer.Status is FoodOfferStatus.Reserved or FoodOfferStatus.Completed or FoodOfferStatus.Cancelled or FoodOfferStatus.Expired)
        {
            return BadRequest(new { message = "Offer is no longer available for acceptance." });
        }

        match.Decision = MatchDecision.Accepted;
        match.RespondedAtUtc = DateTime.UtcNow;
        match.FoodOffer.Status = FoodOfferStatus.Reserved;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notificationPublisher.NotifyAsync(
            ActorType.HospitalityPartner,
            match.FoodOffer.HospitalityPartnerId,
            "Match accepted",
            $"Organization accepted your food offer #{match.FoodOfferId}.",
            cancellationToken,
            NotificationType.MatchAccepted,
            ActorType.CharityOrganization,
            match.CharityOrganizationId);
        return NoContent();
    }

    [HttpPut("{matchId}/cancel")]
    public async Task<IActionResult> CancelAccepted(int matchId, string? reason, CancellationToken cancellationToken)
    {
        var match = await unitOfWork.OfferMatches.GetByIdAsync(matchId, cancellationToken);
        if (match is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.CharityOrganizationId() != match.CharityOrganizationId)
        {
            return Forbid();
        }

        var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(
            match.CharityOrganizationId,
            cancellationToken);

        if (!User.IsAdministrator())
        {
            if (organization is null || organization.ApprovalStatus != ApprovalStatus.Approved)
            {
                return Forbid();
            }
        }

        if (match.Decision != MatchDecision.Accepted)
        {
            return BadRequest(new { message = "Only accepted matches can be cancelled by the organization." });
        }

        var pickupExists = unitOfWork.PickupDocuments.Query().Any(p =>
            p.FoodOfferId == match.FoodOfferId &&
            p.CharityOrganizationId == match.CharityOrganizationId);

        if (pickupExists)
        {
            return BadRequest(new { message = "Accepted match already has a pickup document." });
        }

        var foodOffer = await unitOfWork.FoodOffers.GetByIdAsync(match.FoodOfferId, cancellationToken);
        if (foodOffer is null)
        {
            return NotFound();
        }

        match.Decision = MatchDecision.Cancelled;
        match.DecisionNote = reason;
        match.RespondedAtUtc = DateTime.UtcNow;

        if (organization is not null)
        {
            var previousCancellationCount = organization.CancellationCount;
            organization.CancellationCount++;
            organization.ReputationScore = Math.Max(
                1,
                Math.Round(organization.ReputationScore - 0.2, 2));

            // Fix: cancelling an accepted match is the organization's fault — do NOT grant a
            // MatchCompensationBonus here. The spec states the bonus is compensation for
            // organizations that are victims of partner-side failures (food unavailable, etc.).
            // Granting a bonus to the cancelling org was a copy-paste error that effectively
            // rewarded bad behaviour with higher matching priority.

            await unitOfWork.ReputationSnapshots.AddAsync(
                new ReputationSnapshot
                {
                    ActorType = ActorType.CharityOrganization,
                    ActorId = organization.Id,
                    Score = organization.ReputationScore,
                    Source = "Accepted match cancellation"
                },
                cancellationToken);

            if (previousCancellationCount < 3 && organization.CancellationCount >= 3)
            {
                await notificationPublisher.NotifyAsync(
                    ActorType.Administrator,
                    0,
                    "Cancellation warning",
                    $"Organization #{organization.Id} has reached {organization.CancellationCount} cancellations.",
                    cancellationToken,
                    NotificationType.AdminMessage);
            }
        }

        var nextPending = await NotifyNextPendingMatchAsync(
            foodOffer,
            match.Rank,
            cancellationToken);

        if (nextPending is null)
        {
            foodOffer.Status = FoodOfferStatus.PublicFallback;
        }
        else
        {
            foodOffer.Status = FoodOfferStatus.Active;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationPublisher.NotifyAsync(
            ActorType.HospitalityPartner,
            foodOffer.HospitalityPartnerId,
            "Accepted match cancelled",
            $"Organization cancelled accepted match #{match.Id}.",
            cancellationToken,
            NotificationType.Cancellation,
            ActorType.CharityOrganization,
            match.CharityOrganizationId);

        return NoContent();
    }

    [HttpPost("{matchId}/decline")]
    public async Task<IActionResult> Decline(int matchId, string? reason, CancellationToken cancellationToken)
    {
        var match = await unitOfWork.OfferMatches.GetByIdAsync(matchId, cancellationToken);
        if (match is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.CharityOrganizationId() != match.CharityOrganizationId)
        {
            return Forbid();
        }

        if (!User.IsAdministrator())
        {
            var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(match.CharityOrganizationId, cancellationToken);
            if (organization is null || organization.ApprovalStatus != ApprovalStatus.Approved)
            {
                return Forbid();
            }
        }

        if (match.Decision != MatchDecision.Pending)
        {
            return BadRequest(new { message = "Only pending matches can be declined." });
        }

        match.Decision = MatchDecision.Declined;
        match.DecisionNote = reason;
        match.RespondedAtUtc = DateTime.UtcNow;

        var foodOffer = await unitOfWork.FoodOffers.GetByIdAsync(match.FoodOfferId, cancellationToken);
        if (foodOffer is not null)
        {
            var nextPending = await NotifyNextPendingMatchAsync(foodOffer, match.Rank, cancellationToken);

            if (nextPending is null && foodOffer.Status == FoodOfferStatus.Active)
            {
                foodOffer.Status = FoodOfferStatus.PublicFallback;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<OfferMatch?> NotifyNextPendingMatchAsync(FoodOffer foodOffer, int afterRank, CancellationToken cancellationToken)
    {
        var nextPending = unitOfWork.OfferMatches.Query()
            .Where(m => m.FoodOfferId == foodOffer.Id && m.Decision == MatchDecision.Pending && m.Rank > afterRank)
            .OrderBy(m => m.Rank)
            .FirstOrDefault();

        if (nextPending is null)
        {
            return null;
        }

        nextPending.NotifiedAtUtc = DateTime.UtcNow;
        await notificationPublisher.NotifyAsync(
            ActorType.CharityOrganization,
            nextPending.CharityOrganizationId,
            "Food offer available",
            $"Bite4All offer #{foodOffer.Id} is now available for your organization.",
            cancellationToken,
            NotificationType.MatchingOffer,
            ActorType.HospitalityPartner,
            foodOffer.HospitalityPartnerId);
        return nextPending;
    }
}
