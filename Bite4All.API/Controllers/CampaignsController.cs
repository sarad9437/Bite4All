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

    [Authorize(Roles = "Administrator")]
    [HttpPut("{id}/decision")]
    public async Task<IActionResult> Decide(int id, ApprovalDecisionRequest request, CancellationToken cancellationToken)
    {
        var campaign = await unitOfWork.SpecialCampaigns.GetByIdAsync(id, cancellationToken);
        if (campaign is null)
        {
            return NotFound();
        }

        if (campaign.Status is not (CampaignStatus.Draft or CampaignStatus.PendingApproval))
        {
            return BadRequest(new { message = "Only draft or pending campaigns can be approved or rejected." });
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
