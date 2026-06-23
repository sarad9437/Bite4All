using Bite4All.API.Authentication;
using Bite4All.Application.DTOs.Auth;
using Bite4All.Domain.Enums;
using Bite4All.Domain.Repositories;
using Bite4All.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Bite4All.API.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    IJwtTokenService jwtTokenService,
    IUnitOfWork unitOfWork) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        if (user.ActorType == ActorType.HospitalityPartner)
        {
            var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(user.HospitalityPartnerId ?? 0);
            if (partner is null)
            {
                return Unauthorized(new { message = "Your account is unavailable." });
            }

            // Fix 6: distinguish Suspended from Rejected/Pending so the user gets a
            // meaningful message instead of the generic "waiting for approval".
            if (partner.ApprovalStatus == ApprovalStatus.Suspended)
            {
                return Unauthorized(new
                {
                    message = string.IsNullOrWhiteSpace(partner.RejectionReason)
                        ? "Your account has been suspended. Please contact the platform administrator."
                        : $"Your account has been suspended: {partner.RejectionReason}"
                });
            }

            if (partner.ApprovalStatus != ApprovalStatus.Approved)
            {
                return Unauthorized(new { message = "Your account is waiting for approval." });
            }
        }
        else if (user.ActorType == ActorType.CharityOrganization)
        {
            var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(user.CharityOrganizationId ?? 0);
            if (organization is null)
            {
                return Unauthorized(new { message = "Your account is unavailable." });
            }

            // Fix 6: distinguish Suspended from Rejected/Pending.
            if (organization.ApprovalStatus == ApprovalStatus.Suspended)
            {
                return Unauthorized(new
                {
                    message = string.IsNullOrWhiteSpace(organization.RejectionReason)
                        ? "Your account has been suspended. Please contact the platform administrator."
                        : $"Your account has been suspended: {organization.RejectionReason}"
                });
            }

            if (organization.ApprovalStatus != ApprovalStatus.Approved)
            {
                return Unauthorized(new { message = "Your account is waiting for approval." });
            }
        }
        else if (user.ActorType == ActorType.Driver)
        {
            var driver = await unitOfWork.Drivers.GetByIdAsync(user.DriverId ?? 0);
            if (driver is null)
            {
                return Unauthorized(new { message = "Your account is unavailable." });
            }

            var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(driver.CharityOrganizationId);
            if (organization is null)
            {
                return Unauthorized(new { message = "Your account is unavailable." });
            }

            // Fix 6: a driver whose organization is suspended cannot log in either.
            if (organization.ApprovalStatus == ApprovalStatus.Suspended)
            {
                return Unauthorized(new { message = "Your organization's account has been suspended. Please contact the platform administrator." });
            }

            if (organization.ApprovalStatus != ApprovalStatus.Approved)
            {
                return Unauthorized(new { message = "Your account is waiting for approval." });
            }
        }

        return Ok(await jwtTokenService.CreateTokenAsync(user));
    }
}
