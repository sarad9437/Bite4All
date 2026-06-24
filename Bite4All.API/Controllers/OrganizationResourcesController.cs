using Bite4All.API.Authorization;
using Bite4All.Application.Commands.Drivers;
using Bite4All.Application.Commands.Recipients;
using Bite4All.Application.Commands.Vehicles;
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
    // ----------------------------------------------------------------
    // DRIVERS
    // ----------------------------------------------------------------

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

    [HttpPut("drivers/{driverId}")]
    public async Task<IActionResult> UpdateDriver(int driverId, UpdateDriverRequest request, CancellationToken cancellationToken)
    {
        var driver = await unitOfWork.Drivers.GetByIdAsync(driverId, cancellationToken);
        if (driver is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.CharityOrganizationId() != driver.CharityOrganizationId)
        {
            return Forbid();
        }

        if (!User.IsAdministrator())
        {
            var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(driver.CharityOrganizationId, cancellationToken);
            if (organization is null || organization.ApprovalStatus != ApprovalStatus.Approved)
            {
                return Forbid();
            }
        }

        return await sender.Send(new UpdateDriverCommand(driverId, request.FullName, request.Phone), cancellationToken)
            ? NoContent()
            : NotFound();
    }

    /// <summary>
    /// Suspends a driver account. The driver cannot log in or be assigned to pickups
    /// while suspended. Only the owning organization or an admin can do this.
    /// </summary>
    [HttpPut("drivers/{driverId}/suspend")]
    public async Task<IActionResult> SuspendDriver(int driverId, SuspendRequest request, CancellationToken cancellationToken)
    {
        var driver = await unitOfWork.Drivers.GetByIdAsync(driverId, cancellationToken);
        if (driver is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.CharityOrganizationId() != driver.CharityOrganizationId)
        {
            return Forbid();
        }

        if (!driver.IsActive)
        {
            return BadRequest(new { message = "Driver is already suspended." });
        }

        return await sender.Send(new SuspendDriverCommand(driverId, request.Reason), cancellationToken)
            ? NoContent()
            : NotFound();
    }

    /// <summary>
    /// Reinstates a suspended driver. The driver can log in and be assigned to
    /// pickups again.
    /// </summary>
    [HttpPut("drivers/{driverId}/unsuspend")]
    public async Task<IActionResult> UnsuspendDriver(int driverId, CancellationToken cancellationToken)
    {
        var driver = await unitOfWork.Drivers.GetByIdAsync(driverId, cancellationToken);
        if (driver is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.CharityOrganizationId() != driver.CharityOrganizationId)
        {
            return Forbid();
        }

        if (driver.IsActive)
        {
            return BadRequest(new { message = "Driver is not suspended." });
        }

        return await sender.Send(new UnsuspendDriverCommand(driverId), cancellationToken)
            ? NoContent()
            : NotFound();
    }

    /// <summary>
    /// Permanently deactivates a driver. Cannot be undone. Driver must not be
    /// assigned to any active pickup.
    /// </summary>
    [HttpDelete("drivers/{driverId}")]
    public async Task<IActionResult> DeactivateDriver(int driverId, CancellationToken cancellationToken)
    {
        var driver = await unitOfWork.Drivers.GetByIdAsync(driverId, cancellationToken);
        if (driver is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.CharityOrganizationId() != driver.CharityOrganizationId)
        {
            return Forbid();
        }

        if (!User.IsAdministrator())
        {
            var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(driver.CharityOrganizationId, cancellationToken);
            if (organization is null || organization.ApprovalStatus != ApprovalStatus.Approved)
            {
                return Forbid();
            }
        }

        var hasActivePickup = unitOfWork.PickupDocuments.Query().Any(p =>
            p.DriverId == driverId &&
            p.Status != PickupStatus.Cancelled &&
            p.Status != PickupStatus.DeliveredToOrganization);

        if (hasActivePickup)
        {
            return BadRequest(new { message = "Driver cannot be deactivated while assigned to an active pickup." });
        }

        driver.IsActive = false;
        driver.IsAvailable = false;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // ----------------------------------------------------------------
    // VEHICLES
    // ----------------------------------------------------------------

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

    [HttpPut("vehicles/{vehicleId}")]
    public async Task<IActionResult> UpdateVehicle(int vehicleId, UpdateVehicleRequest request, CancellationToken cancellationToken)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(vehicleId, cancellationToken);
        if (vehicle is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.CharityOrganizationId() != vehicle.CharityOrganizationId)
        {
            return Forbid();
        }

        if (!User.IsAdministrator())
        {
            var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(vehicle.CharityOrganizationId, cancellationToken);
            if (organization is null || organization.ApprovalStatus != ApprovalStatus.Approved)
            {
                return Forbid();
            }
        }

        var (success, error) = await sender.Send(
            new UpdateVehicleCommand(vehicleId, request.RegistrationNumber, request.CapacityKg, request.HasCooling),
            cancellationToken);

        return success ? NoContent() : BadRequest(new { message = error });
    }

    /// <summary>
    /// Permanently deactivates a vehicle. Cannot be undone. Vehicle must not be
    /// assigned to any active pickup.
    /// </summary>
    [HttpDelete("vehicles/{vehicleId}")]
    public async Task<IActionResult> DeactivateVehicle(int vehicleId, CancellationToken cancellationToken)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(vehicleId, cancellationToken);
        if (vehicle is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.CharityOrganizationId() != vehicle.CharityOrganizationId)
        {
            return Forbid();
        }

        if (!User.IsAdministrator())
        {
            var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(vehicle.CharityOrganizationId, cancellationToken);
            if (organization is null || organization.ApprovalStatus != ApprovalStatus.Approved)
            {
                return Forbid();
            }
        }

        var (success, error) = await sender.Send(new DeactivateVehicleCommand(vehicleId), cancellationToken);
        return success ? NoContent() : BadRequest(new { message = error });
    }

    // ----------------------------------------------------------------
    // RECIPIENTS
    // ----------------------------------------------------------------

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

    /// <summary>
    /// Returns full list of recipients for the organization — including their
    /// internal codes and dietary restrictions — for internal management.
    /// Only the owning organization and admins can access this.
    /// </summary>
    [HttpGet("{organizationId}/recipients/list")]
    public async Task<ActionResult<List<Recipient>>> GetRecipientsList(int organizationId, CancellationToken cancellationToken)
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

        return Ok(unitOfWork.Recipients.Query()
            .Where(r => r.CharityOrganizationId == organizationId)
            .OrderBy(r => r.InternalCode)
            .ToList());
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
        organization.TemporaryCapacityExpiresAtUtc = null;
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

    /// <summary>
    /// Updates the dietary restrictions of a recipient.
    /// </summary>
    [HttpPut("recipients/{recipientId}")]
    public async Task<IActionResult> UpdateRecipient(int recipientId, UpdateRecipientRequest request, CancellationToken cancellationToken)
    {
        var recipient = await unitOfWork.Recipients.GetByIdAsync(recipientId, cancellationToken);
        if (recipient is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.CharityOrganizationId() != recipient.CharityOrganizationId)
        {
            return Forbid();
        }

        if (!User.IsAdministrator())
        {
            var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(recipient.CharityOrganizationId, cancellationToken);
            if (organization is null || organization.ApprovalStatus != ApprovalStatus.Approved)
            {
                return Forbid();
            }
        }

        return await sender.Send(new UpdateRecipientCommand(recipientId, request.DietaryRestrictions), cancellationToken)
            ? NoContent()
            : NotFound();
    }

    /// <summary>
    /// Soft-deletes a recipient. Historical distribution records are preserved.
    /// Deactivated recipients are excluded from new distributions and dietary matching.
    /// </summary>
    [HttpDelete("recipients/{recipientId}")]
    public async Task<IActionResult> DeactivateRecipient(int recipientId, CancellationToken cancellationToken)
    {
        var recipient = await unitOfWork.Recipients.GetByIdAsync(recipientId, cancellationToken);
        if (recipient is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.CharityOrganizationId() != recipient.CharityOrganizationId)
        {
            return Forbid();
        }

        if (!User.IsAdministrator())
        {
            var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(recipient.CharityOrganizationId, cancellationToken);
            if (organization is null || organization.ApprovalStatus != ApprovalStatus.Approved)
            {
                return Forbid();
            }
        }

        return await sender.Send(new DeactivateRecipientCommand(recipientId), cancellationToken)
            ? NoContent()
            : NotFound();
    }
}
