using Bite4All.API.Hubs;
using Bite4All.Application.DTOs.Onboarding;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Domain.Repositories;
using Bite4All.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Bite4All.API.Controllers;

[ApiController]
[Route("onboarding")]
public class OnboardingController(
    IUnitOfWork unitOfWork,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    INotificationPublisher notificationPublisher) : ControllerBase
{
    [HttpPost("hospitality-partners")]
    public async Task<ActionResult<HospitalityPartner>> RegisterPartner(RegisterHospitalityPartnerRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Password is required for the partner account." });
        }

        if (await userManager.FindByEmailAsync(request.ContactEmail) is not null)
        {
            return BadRequest(new { message = "A user with this email already exists." });
        }

        var partner = new HospitalityPartner
        {
            Name = request.Name,
            PartnerType = request.PartnerType,
            Address = request.Address,
            CityId = request.CityId,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            TaxIdentificationNumber = request.TaxIdentificationNumber,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            ApprovalStatus = ApprovalStatus.Pending
        };

        await unitOfWork.HospitalityPartners.AddAsync(partner, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var userResult = await CreateActorUserAsync(
            request.ContactEmail,
            request.Password,
            "HospitalityPartner",
            ActorType.HospitalityPartner,
            partner.Id,
            null,
            null);

        if (!userResult.Succeeded)
        {
            unitOfWork.HospitalityPartners.Delete(partner);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return BadRequest(userResult.Errors.Select(e => e.Description));
        }

        return CreatedAtAction(nameof(GetPendingRegistrations), new { id = partner.Id }, partner);
    }

    [HttpPost("organizations")]
    public async Task<ActionResult<CharityOrganization>> RegisterOrganization(RegisterOrganizationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Password is required for the organization account." });
        }

        if (await userManager.FindByEmailAsync(request.ContactEmail) is not null)
        {
            return BadRequest(new { message = "A user with this email already exists." });
        }

        var organization = new CharityOrganization
        {
            Name = request.Name,
            Address = request.Address,
            CityId = request.CityId,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            TaxIdentificationNumber = request.TaxIdentificationNumber,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            DailyCapacityKg = request.DailyCapacityKg,
            ApprovalStatus = ApprovalStatus.Pending
        };

        await unitOfWork.CharityOrganizations.AddAsync(organization, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var userResult = await CreateActorUserAsync(
            request.ContactEmail,
            request.Password,
            "CharityOrganization",
            ActorType.CharityOrganization,
            null,
            organization.Id,
            null);

        if (!userResult.Succeeded)
        {
            unitOfWork.CharityOrganizations.Delete(organization);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return BadRequest(userResult.Errors.Select(e => e.Description));
        }

        return CreatedAtAction(nameof(GetPendingRegistrations), new { id = organization.Id }, organization);
    }

    [Authorize(Roles = "Administrator")]
    [HttpGet("pending")]
    public async Task<ActionResult<object>> GetPendingRegistrations(CancellationToken cancellationToken)
    {
        var partners = unitOfWork.HospitalityPartners.Query()
            .Where(p => p.ApprovalStatus == ApprovalStatus.Pending)
            .ToList();
        var organizations = unitOfWork.CharityOrganizations.Query()
            .Where(o => o.ApprovalStatus == ApprovalStatus.Pending)
            .ToList();

        return Ok(new { partners, organizations });
    }

    [Authorize(Roles = "Administrator")]
    [HttpPut("hospitality-partners/{id}/decision")]
    public async Task<IActionResult> DecidePartner(int id, ApprovalDecisionRequest request, CancellationToken cancellationToken)
    {
        var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(id, cancellationToken);
        if (partner is null)
        {
            return NotFound();
        }

        partner.ApprovalStatus = request.Approved ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        partner.RejectionReason = request.Approved ? null : request.Reason;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notificationPublisher.NotifyAsync(
            ActorType.HospitalityPartner,
            partner.Id,
            "Registration decision",
            request.Approved ? "Your Bite4All partner registration was approved." : $"Your Bite4All partner registration was rejected: {request.Reason}",
            cancellationToken,
            NotificationType.RegistrationDecision);
        return NoContent();
    }

    [Authorize(Roles = "Administrator")]
    [HttpPut("organizations/{id}/decision")]
    public async Task<IActionResult> DecideOrganization(int id, ApprovalDecisionRequest request, CancellationToken cancellationToken)
    {
        var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(id, cancellationToken);
        if (organization is null)
        {
            return NotFound();
        }

        organization.ApprovalStatus = request.Approved ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        organization.RejectionReason = request.Approved ? null : request.Reason;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notificationPublisher.NotifyAsync(
            ActorType.CharityOrganization,
            organization.Id,
            "Registration decision",
            request.Approved ? "Your Bite4All organization registration was approved." : $"Your Bite4All organization registration was rejected: {request.Reason}",
            cancellationToken,
            NotificationType.RegistrationDecision);
        return NoContent();
    }

    /// <summary>
    /// Fix 6: suspends an approved hospitality partner — blocks login and prevents
    /// new offers while preserving all historical data. Admins should use this
    /// for temporary bans rather than Rejected (which is permanent).
    /// </summary>
    [Authorize(Roles = "Administrator")]
    [HttpPut("hospitality-partners/{id}/suspend")]
    public async Task<IActionResult> SuspendPartner(int id, SuspendRequest request, CancellationToken cancellationToken)
    {
        var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(id, cancellationToken);
        if (partner is null)
        {
            return NotFound();
        }

        if (partner.ApprovalStatus == ApprovalStatus.Suspended)
        {
            return BadRequest(new { message = "Partner is already suspended." });
        }

        partner.ApprovalStatus = ApprovalStatus.Suspended;
        partner.RejectionReason = request.Reason;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notificationPublisher.NotifyAsync(
            ActorType.HospitalityPartner,
            partner.Id,
            "Account suspended",
            $"Your Bite4All account has been suspended: {request.Reason}",
            cancellationToken,
            NotificationType.RegistrationDecision);
        return NoContent();
    }

    /// <summary>
    /// Fix 6: reinstates a suspended hospitality partner. The account is moved back to
    /// Approved so the partner can log in and create offers again.
    /// </summary>
    [Authorize(Roles = "Administrator")]
    [HttpPut("hospitality-partners/{id}/unsuspend")]
    public async Task<IActionResult> UnsuspendPartner(int id, CancellationToken cancellationToken)
    {
        var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(id, cancellationToken);
        if (partner is null)
        {
            return NotFound();
        }

        if (partner.ApprovalStatus != ApprovalStatus.Suspended)
        {
            return BadRequest(new { message = "Partner is not suspended." });
        }

        partner.ApprovalStatus = ApprovalStatus.Approved;
        partner.RejectionReason = null;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notificationPublisher.NotifyAsync(
            ActorType.HospitalityPartner,
            partner.Id,
            "Account reinstated",
            "Your Bite4All account has been reinstated. You can now log in and create offers again.",
            cancellationToken,
            NotificationType.RegistrationDecision);
        return NoContent();
    }

    /// <summary>
    /// Fix 6: suspends an approved charity organization.
    /// </summary>
    [Authorize(Roles = "Administrator")]
    [HttpPut("organizations/{id}/suspend")]
    public async Task<IActionResult> SuspendOrganization(int id, SuspendRequest request, CancellationToken cancellationToken)
    {
        var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(id, cancellationToken);
        if (organization is null)
        {
            return NotFound();
        }

        if (organization.ApprovalStatus == ApprovalStatus.Suspended)
        {
            return BadRequest(new { message = "Organization is already suspended." });
        }

        organization.ApprovalStatus = ApprovalStatus.Suspended;
        organization.RejectionReason = request.Reason;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notificationPublisher.NotifyAsync(
            ActorType.CharityOrganization,
            organization.Id,
            "Account suspended",
            $"Your Bite4All organization account has been suspended: {request.Reason}",
            cancellationToken,
            NotificationType.RegistrationDecision);
        return NoContent();
    }

    /// <summary>
    /// Fix 6: reinstates a suspended charity organization.
    /// </summary>
    [Authorize(Roles = "Administrator")]
    [HttpPut("organizations/{id}/unsuspend")]
    public async Task<IActionResult> UnsuspendOrganization(int id, CancellationToken cancellationToken)
    {
        var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(id, cancellationToken);
        if (organization is null)
        {
            return NotFound();
        }

        if (organization.ApprovalStatus != ApprovalStatus.Suspended)
        {
            return BadRequest(new { message = "Organization is not suspended." });
        }

        organization.ApprovalStatus = ApprovalStatus.Approved;
        organization.RejectionReason = null;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notificationPublisher.NotifyAsync(
            ActorType.CharityOrganization,
            organization.Id,
            "Account reinstated",
            "Your Bite4All organization account has been reinstated.",
            cancellationToken,
            NotificationType.RegistrationDecision);
        return NoContent();
    }

    private async Task<IdentityResult> CreateActorUserAsync(
        string email,
        string password,
        string role,
        ActorType actorType,
        int? hospitalityPartnerId,
        int? charityOrganizationId,
        int? driverId)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
            if (!roleResult.Succeeded)
            {
                return roleResult;
            }
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            ActorType = actorType,
            HospitalityPartnerId = hospitalityPartnerId,
            CharityOrganizationId = charityOrganizationId,
            DriverId = driverId
        };

        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            return createResult;
        }

        var addToRoleResult = await userManager.AddToRoleAsync(user, role);
        if (!addToRoleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return addToRoleResult;
        }

        return addToRoleResult;
    }
}
