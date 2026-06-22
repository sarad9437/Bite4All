using Bite4All.API.Authorization;
using Bite4All.Application.DTOs.Onboarding;
using Bite4All.Application.DTOs.Organizations;
using Bite4All.Application.Queries.Organizations;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Domain.Repositories;
using Bite4All.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Bite4All.API.Controllers;

[ApiController]
[Authorize(Roles = "CharityOrganization,Administrator")]
[Route("organizations")]
public class OrganizationResourcesController(
    IUnitOfWork unitOfWork,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ISender sender) : ControllerBase
{
    [HttpGet("{organizationId}/drivers")]
    public async Task<ActionResult<List<Driver>>> GetDrivers(int organizationId, CancellationToken cancellationToken)
    {
        var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(organizationId, cancellationToken);
        if (organization is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.CharityOrganizationId() != organizationId)
        {
            return Forbid();
        }

        if (!User.IsAdministrator() && organization.ApprovalStatus != ApprovalStatus.Approved)
        {
            return Forbid();
        }

        return Ok(unitOfWork.Drivers.Query().Where(d => d.CharityOrganizationId == organizationId).ToList());
    }

    [HttpPost("drivers")]
    public async Task<ActionResult<Driver>> CreateDriver(CreateDriverRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsAdministrator() && User.CharityOrganizationId() != request.CharityOrganizationId)
        {
            return Forbid();
        }

        var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(request.CharityOrganizationId, cancellationToken);
        if (organization is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && organization.ApprovalStatus != ApprovalStatus.Approved)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Password is required for the driver account." });
        }

        if (await userManager.FindByEmailAsync(request.Email) is not null)
        {
            return BadRequest(new { message = "A user with this email already exists." });
        }

        var driver = new Driver
        {
            CharityOrganizationId = request.CharityOrganizationId,
            FullName = request.FullName,
            Email = request.Email,
            Phone = request.Phone
        };
        await unitOfWork.Drivers.AddAsync(driver, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (!await roleManager.RoleExistsAsync("Driver"))
        {
            await roleManager.CreateAsync(new IdentityRole("Driver"));
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
            ActorType = ActorType.Driver,
            CharityOrganizationId = request.CharityOrganizationId,
            DriverId = driver.Id
        };

        var userResult = await userManager.CreateAsync(user, request.Password);
        if (!userResult.Succeeded)
        {
            unitOfWork.Drivers.Delete(driver);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return BadRequest(userResult.Errors.Select(e => e.Description));
        }

        var addToRoleResult = await userManager.AddToRoleAsync(user, "Driver");
        if (!addToRoleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            unitOfWork.Drivers.Delete(driver);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return BadRequest(addToRoleResult.Errors.Select(e => e.Description));
        }

        return Ok(driver);
    }

    [HttpPost("vehicles")]
    public async Task<ActionResult<Vehicle>> CreateVehicle(CreateVehicleRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsAdministrator() && User.CharityOrganizationId() != request.CharityOrganizationId)
        {
            return Forbid();
        }

        var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(request.CharityOrganizationId, cancellationToken);
        if (organization is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && organization.ApprovalStatus != ApprovalStatus.Approved)
        {
            return Forbid();
        }

        var vehicle = new Vehicle
        {
            CharityOrganizationId = request.CharityOrganizationId,
            RegistrationNumber = request.RegistrationNumber,
            CapacityKg = request.CapacityKg,
            HasCooling = request.HasCooling
        };
        await unitOfWork.Vehicles.AddAsync(vehicle, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(vehicle);
    }

    [HttpGet("{organizationId}/vehicles")]
    public async Task<ActionResult<List<Vehicle>>> GetVehicles(int organizationId, CancellationToken cancellationToken)
    {
        var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(organizationId, cancellationToken);
        if (organization is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.CharityOrganizationId() != organizationId)
        {
            return Forbid();
        }

        if (!User.IsAdministrator() && organization.ApprovalStatus != ApprovalStatus.Approved)
        {
            return Forbid();
        }

        return Ok(unitOfWork.Vehicles.Query().Where(v => v.CharityOrganizationId == organizationId).ToList());
    }

    [HttpPost("recipients")]
    public async Task<ActionResult<Recipient>> CreateRecipient(CreateRecipientRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsAdministrator() && User.CharityOrganizationId() != request.CharityOrganizationId)
        {
            return Forbid();
        }

        var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(request.CharityOrganizationId, cancellationToken);
        if (organization is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && organization.ApprovalStatus != ApprovalStatus.Approved)
        {
            return Forbid();
        }

        var recipient = new Recipient
        {
            CharityOrganizationId = request.CharityOrganizationId,
            InternalCode = request.InternalCode,
            DietaryRestrictions = request.DietaryRestrictions
        };
        await unitOfWork.Recipients.AddAsync(recipient, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(recipient);
    }

    [HttpPut("{organizationId}/temporary-capacity")]
    public async Task<IActionResult> UpdateTemporaryCapacity(int organizationId, UpdateTemporaryCapacityRequest request, CancellationToken cancellationToken)
    {
        var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(organizationId, cancellationToken);
        if (organization is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.CharityOrganizationId() != organizationId)
        {
            return Forbid();
        }

        if (!User.IsAdministrator() && organization.ApprovalStatus != ApprovalStatus.Approved)
        {
            return Forbid();
        }

        if (request.TemporaryExtraCapacityKg < 0)
        {
            return BadRequest(new { message = "Temporary capacity cannot be negative." });
        }

        organization.TemporaryExtraCapacityKg = request.TemporaryExtraCapacityKg;
        organization.TemporaryCapacityExpiresAtUtc = request.ExpiresAtUtc;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{organizationId}/temporary-capacity")]
    public async Task<IActionResult> ClearTemporaryCapacity(int organizationId, CancellationToken cancellationToken)
    {
        var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(organizationId, cancellationToken);
        if (organization is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.CharityOrganizationId() != organizationId)
        {
            return Forbid();
        }

        if (!User.IsAdministrator() && organization.ApprovalStatus != ApprovalStatus.Approved)
        {
            return Forbid();
        }

        organization.TemporaryExtraCapacityKg = 0;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{organizationId}/recipients")]
    public async Task<ActionResult<RecipientPrivacySummaryDto>> GetRecipients(int organizationId, CancellationToken cancellationToken)
    {
        var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(organizationId, cancellationToken);
        if (organization is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.CharityOrganizationId() != organizationId)
        {
            return Forbid();
        }

        if (!User.IsAdministrator() && organization.ApprovalStatus != ApprovalStatus.Approved)
        {
            return Forbid();
        }

        var summary = await sender.Send(new GetRecipientPrivacySummaryQuery(organizationId), cancellationToken);
        return summary is null ? NotFound() : Ok(summary);
    }
}
