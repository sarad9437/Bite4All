using Bite4All.API.Authorization;
using Bite4All.Application.DTOs.Recipients;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bite4All.API.Controllers;

[ApiController]
[Authorize(Roles = "CharityOrganization,Administrator")]
[Route("recipient-distributions")]
public class RecipientDistributionsController(IUnitOfWork unitOfWork) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<RecipientMealDistribution>> Record(RecordMealDistributionRequest request, CancellationToken cancellationToken)
    {
        if (request.MealsCount <= 0)
        {
            return BadRequest(new { message = "Meals count must be greater than zero." });
        }

        var pickup = await unitOfWork.PickupDocuments.GetByIdAsync(request.PickupDocumentId, cancellationToken);
        var recipient = await unitOfWork.Recipients.GetByIdAsync(request.RecipientId, cancellationToken);
        if (pickup is null || recipient is null)
        {
            return NotFound();
        }

        // Fix: deactivated recipients cannot receive new meal distributions.
        // Their historical records are preserved but they must not appear in new entries.
        if (!recipient.IsActive)
        {
            return BadRequest(new { message = "Recipient is deactivated and cannot receive new meal distributions." });
        }

        if (pickup.Status is not (PickupStatus.PickedUp or PickupStatus.DeliveredToOrganization))
        {
            return BadRequest(new { message = "Meals can be recorded only after food is picked up or delivered." });
        }

        if (pickup.CharityOrganizationId != recipient.CharityOrganizationId)
        {
            return BadRequest(new { message = "Recipient must belong to the pickup organization." });
        }

        var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(pickup.CharityOrganizationId, cancellationToken);
        if (organization is null || organization.ApprovalStatus != ApprovalStatus.Approved)
        {
            return Forbid();
        }

        if (!User.IsAdministrator() && User.CharityOrganizationId() != pickup.CharityOrganizationId)
        {
            return Forbid();
        }

        // Prevent duplicate distribution records for the same (pickup, recipient) pair.
        var alreadyRecorded = unitOfWork.RecipientMealDistributions.Query().Any(d =>
            d.PickupDocumentId == request.PickupDocumentId &&
            d.RecipientId == request.RecipientId);

        if (alreadyRecorded)
        {
            return Conflict(new { message = "A meal distribution for this recipient and pickup has already been recorded." });
        }

        var distribution = new RecipientMealDistribution
        {
            PickupDocumentId = pickup.Id,
            RecipientId = recipient.Id,
            CharityOrganizationId = pickup.CharityOrganizationId,
            Category = request.Category,
            MealsCount = request.MealsCount
        };

        recipient.MealsReceivedCount += request.MealsCount;
        await unitOfWork.RecipientMealDistributions.AddAsync(distribution, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(distribution);
    }

    [HttpGet("organization/{charityOrganizationId:int}")]
    public ActionResult<List<RecipientMealDistribution>> GetForOrganization(int charityOrganizationId)
    {
        if (!User.IsAdministrator() && User.CharityOrganizationId() != charityOrganizationId)
        {
            return Forbid();
        }

        var organization = unitOfWork.CharityOrganizations.Query().FirstOrDefault(o => o.Id == charityOrganizationId);
        if (!User.IsAdministrator() && (organization is null || organization.ApprovalStatus != ApprovalStatus.Approved))
        {
            return Forbid();
        }

        return Ok(unitOfWork.RecipientMealDistributions.Query()
            .Where(d => d.CharityOrganizationId == charityOrganizationId)
            .OrderByDescending(d => d.DistributedAtUtc)
            .ToList());
    }
}
