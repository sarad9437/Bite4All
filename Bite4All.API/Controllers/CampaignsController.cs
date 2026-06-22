using Bite4All.API.Authorization;
using Bite4All.API.Hubs;
using Bite4All.Application.DTOs.FoodOffers;
using Bite4All.Application.DTOs.Onboarding;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bite4All.API.Controllers;

[ApiController]
[Route("campaigns")]
public class CampaignsController(IUnitOfWork unitOfWork, INotificationPublisher notificationPublisher) : ControllerBase
{
    [HttpGet("featured")]
    public async Task<ActionResult<List<SpecialCampaign>>> GetFeatured(CancellationToken cancellationToken)
    {
        return Ok(unitOfWork.SpecialCampaigns.Query()
            .Where(c => c.Status == CampaignStatus.Featured || c.Status == CampaignStatus.Approved)
            .OrderBy(c => c.EndsOn)
            .ToList());
    }

    [Authorize(Roles = "Administrator")]
    [HttpGet("pending")]
    public async Task<ActionResult<List<SpecialCampaign>>> GetPending(CancellationToken cancellationToken)
    {
        return Ok(unitOfWork.SpecialCampaigns.Query()
            .Where(c => c.Status == CampaignStatus.Draft || c.Status == CampaignStatus.PendingApproval)
            .OrderBy(c => c.StartsOn)
            .ToList());
    }

    /// <summary>
    /// Creates a campaign as a Draft (not yet submitted for approval).
    /// The partner can review and edit it before submitting.
    /// </summary>
    [Authorize(Roles = "HospitalityPartner,Administrator")]
    [HttpPost("draft")]
    public async Task<ActionResult<SpecialCampaign>> CreateDraft(CreateCampaignRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsAdministrator() && User.HospitalityPartnerId() != request.HospitalityPartnerId)
        {
            return Forbid();
        }

        var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(request.HospitalityPartnerId, cancellationToken);
        if (partner is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && partner.ApprovalStatus != ApprovalStatus.Approved)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.TargetQuantityKg <= 0 || request.EndsOn < request.StartsOn)
        {
            return BadRequest(new { message = "Campaign name, valid date range and positive target quantity are required." });
        }

        var campaign = new SpecialCampaign
        {
            HospitalityPartnerId = request.HospitalityPartnerId,
            Name = request.Name,
            Description = request.Description,
            StartsOn = request.StartsOn,
            EndsOn = request.EndsOn,
            TargetQuantityKg = request.TargetQuantityKg,
            Status = CampaignStatus.Draft
        };
        await unitOfWork.SpecialCampaigns.AddAsync(campaign, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(campaign);
    }

    /// <summary>
    /// Creates a campaign and immediately submits it for admin approval.
    /// </summary>
    [Authorize(Roles = "HospitalityPartner,Administrator")]
    [HttpPost]
    public async Task<ActionResult<SpecialCampaign>> Create(CreateCampaignRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsAdministrator() && User.HospitalityPartnerId() != request.HospitalityPartnerId)
        {
            return Forbid();
        }

        var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(request.HospitalityPartnerId, cancellationToken);
        if (partner is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && partner.ApprovalStatus != ApprovalStatus.Approved)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.TargetQuantityKg <= 0 || request.EndsOn < request.StartsOn)
        {
            return BadRequest(new { message = "Campaign name, valid date range and positive target quantity are required." });
        }

        var campaign = new SpecialCampaign
        {
            HospitalityPartnerId = request.HospitalityPartnerId,
            Name = request.Name,
            Description = request.Description,
            StartsOn = request.StartsOn,
            EndsOn = request.EndsOn,
            TargetQuantityKg = request.TargetQuantityKg,
            Status = CampaignStatus.PendingApproval
        };
        await unitOfWork.SpecialCampaigns.AddAsync(campaign, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notificationPublisher.NotifyAsync(
            ActorType.Administrator,
            0,
            "Campaign awaiting approval",
            $"Hospitality partner #{request.HospitalityPartnerId} submitted a campaign for review: {campaign.Name}.",
            cancellationToken,
            NotificationType.AdminMessage);
        return Ok(campaign);
    }

    /// <summary>
    /// Submits an existing Draft campaign for admin approval.
    /// Only the owning partner or admin can do this.
    /// </summary>
    [Authorize(Roles = "HospitalityPartner,Administrator")]
    [HttpPut("{id}/submit")]
    public async Task<IActionResult> Submit(int id, CancellationToken cancellationToken)
    {
        var campaign = await unitOfWork.SpecialCampaigns.GetByIdAsync(id, cancellationToken);
        if (campaign is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.HospitalityPartnerId() != campaign.HospitalityPartnerId)
        {
            return Forbid();
        }

        if (campaign.Status != CampaignStatus.Draft)
        {
            return BadRequest(new { message = "Only draft campaigns can be submitted for approval." });
        }

        campaign.Status = CampaignStatus.PendingApproval;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notificationPublisher.NotifyAsync(
            ActorType.Administrator,
            0,
            "Campaign awaiting approval",
            $"Hospitality partner #{campaign.HospitalityPartnerId} submitted campaign \"{campaign.Name}\" for review.",
            cancellationToken,
            NotificationType.AdminMessage);
        return NoContent();
    }

    /// <summary>
    /// Approves or rejects a campaign that is in PendingApproval status.
    /// Fix: only PendingApproval campaigns can be decided — Draft campaigns must first be
    /// submitted via PUT /{id}/submit. This prevents admins from accidentally approving
    /// a draft that the partner has not yet finished editing.
    /// </summary>
    [Authorize(Roles = "Administrator")]
    [HttpPut("{id}/decision")]
    public async Task<IActionResult> Decide(int id, ApprovalDecisionRequest request, CancellationToken cancellationToken)
    {
        var campaign = await unitOfWork.SpecialCampaigns.GetByIdAsync(id, cancellationToken);
        if (campaign is null)
        {
            return NotFound();
        }

        // Fix: was `Draft or PendingApproval` — Draft should NOT be directly approvable.
        // The partner must explicitly submit it first (PUT /{id}/submit).
        if (campaign.Status != CampaignStatus.PendingApproval)
        {
            return BadRequest(new { message = "Only campaigns that have been submitted for approval can be approved or rejected." });
        }

        campaign.Status = request.Approved ? CampaignStatus.Approved : CampaignStatus.Rejected;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notificationPublisher.NotifyAsync(
            ActorType.HospitalityPartner,
            campaign.HospitalityPartnerId,
            "Campaign decision",
            request.Approved
                ? $"Your campaign \"{campaign.Name}\" was approved."
                : $"Your campaign \"{campaign.Name}\" was rejected: {request.Reason}",
            cancellationToken,
            NotificationType.RegistrationDecision);
        return NoContent();
    }

    [Authorize(Roles = "Administrator")]
    [HttpPut("{id}/feature")]
    public async Task<IActionResult> Feature(int id, CancellationToken cancellationToken)
    {
        var campaign = await unitOfWork.SpecialCampaigns.GetByIdAsync(id, cancellationToken);
        if (campaign is null)
        {
            return NotFound();
        }

        if (campaign.Status != CampaignStatus.Approved)
        {
            return BadRequest(new { message = "Only approved campaigns can be featured." });
        }

        campaign.Status = CampaignStatus.Featured;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notificationPublisher.NotifyAsync(
            ActorType.HospitalityPartner,
            campaign.HospitalityPartnerId,
            "Campaign featured",
            $"Your campaign \"{campaign.Name}\" is now featured platform-wide.",
            cancellationToken,
            NotificationType.RegistrationDecision);
        return NoContent();
    }

    [Authorize(Roles = "Administrator")]
    [HttpPut("{id}/status")]
    public async Task<IActionResult> ChangeStatus(int id, CampaignStatus status, CancellationToken cancellationToken)
    {
        var campaign = await unitOfWork.SpecialCampaigns.GetByIdAsync(id, cancellationToken);
        if (campaign is null)
        {
            return NotFound();
        }

        campaign.Status = status;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
