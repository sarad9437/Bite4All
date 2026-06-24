using Bite4All.API.Authorization;
using Bite4All.API.Hubs;
using Bite4All.API.Validators;
using Bite4All.Application.DTOs.Common;
using Bite4All.Application.DTOs.Pickups;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Domain.Repositories;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Bite4All.API.Controllers;

[ApiController]
[Route("pickups")]
public class PickupsController(
    IUnitOfWork unitOfWork,
    IValidator<AssignPickupRequest> assignValidator,
    IValidator<CompletePickupRequest> completeValidator,
    INotificationPublisher notificationPublisher,
    IHubContext<NotificationsHub> hubContext) : ControllerBase
{
    private static readonly PickupStatus[] ActiveStatuses =
    [
        PickupStatus.Created,
        PickupStatus.Assigned,
        PickupStatus.DriverConfirmed,
        PickupStatus.PickedUp,
        PickupStatus.ProblemReported
    ];

    // Statuses on which it makes no sense to report a new issue.
    private static readonly PickupStatus[] NonReportableStatuses =
    [
        PickupStatus.PickedUp,
        PickupStatus.DeliveredToOrganization,
        PickupStatus.Cancelled,
        PickupStatus.ProblemReported
    ];

    [Authorize]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PickupDocumentDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var pickup = await unitOfWork.PickupDocuments.GetByIdAsync(id, cancellationToken);
        if (pickup is null)
        {
            return NotFound();
        }

        if (!CanAccessPickup(pickup))
        {
            return Forbid();
        }

        await LoadPickupDetailsAsync(pickup, cancellationToken);
        return Ok(ToDto(pickup));
    }

    [Authorize(Roles = "Administrator")]
    [HttpGet]
    public ActionResult<PagedResult<PickupDocumentDto>> GetAll(
        [FromQuery] PickupStatus? status = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = unitOfWork.PickupDocuments.Query().AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(p => p.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(p => p.CreatedAtUtc <= toUtc.Value);
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var totalCount = query.Count();
        var pickups = query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new PagedResult<PickupDocumentDto>
        {
            Items = pickups.Select(p => ToDto(p)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    [Authorize(Roles = "Driver,Administrator")]
    [HttpGet("driver/{driverId:int}")]
    public async Task<ActionResult<List<PickupDocumentDto>>> GetForDriver(
        int driverId,
        [FromQuery] bool activeOnly = false,
        [FromQuery] PickupStatus? status = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (!User.IsAdministrator() && User.DriverId() != driverId)
        {
            return Forbid();
        }

        var query = unitOfWork.PickupDocuments.Query()
            .Where(p => p.DriverId == driverId);

        if (activeOnly)
        {
            query = query.Where(p => ActiveStatuses.Contains(p.Status));
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(p => p.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(p => p.CreatedAtUtc <= toUtc.Value);
        }

        var pickups = query
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToList();

        foreach (var pickup in pickups)
        {
            await LoadPickupDetailsAsync(pickup, cancellationToken);
        }

        return Ok(pickups.Select(ToDto).ToList());
    }

    [Authorize(Roles = "CharityOrganization,Administrator")]
    [HttpGet("organization/{organizationId:int}")]
    public async Task<ActionResult<List<PickupDocumentDto>>> GetForOrganization(
        int organizationId,
        [FromQuery] PickupStatus? status = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (!User.IsAdministrator() && User.CharityOrganizationId() != organizationId)
        {
            return Forbid();
        }

        var query = unitOfWork.PickupDocuments.Query()
            .Where(p => p.CharityOrganizationId == organizationId);

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(p => p.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(p => p.CreatedAtUtc <= toUtc.Value);
        }

        var pickups = query
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToList();

        foreach (var pickup in pickups)
        {
            await LoadPickupDetailsAsync(pickup, cancellationToken);
        }

        return Ok(pickups.Select(ToDto).ToList());
    }

    [Authorize(Roles = "HospitalityPartner,Administrator")]
    [HttpGet("partner/{hospitalityPartnerId:int}")]
    public async Task<ActionResult<List<PickupDocumentDto>>> GetForPartner(
        int hospitalityPartnerId,
        [FromQuery] PickupStatus? status = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (!User.IsAdministrator() && User.HospitalityPartnerId() != hospitalityPartnerId)
        {
            return Forbid();
        }

        var query = unitOfWork.PickupDocuments.Query()
            .Where(p => p.HospitalityPartnerId == hospitalityPartnerId);

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(p => p.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(p => p.CreatedAtUtc <= toUtc.Value);
        }

        var pickups = query
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToList();

        foreach (var pickup in pickups)
        {
            await LoadPickupDetailsAsync(pickup, cancellationToken);
        }

        return Ok(pickups.Select(ToDto).ToList());
    }

    [Authorize(Roles = "CharityOrganization,Administrator")]
    [HttpPost("from-match/{matchId}")]
    public async Task<ActionResult<PickupDocumentDto>> CreateFromMatch(int matchId, CancellationToken cancellationToken)
    {
        var match = unitOfWork.OfferMatches.Query().FirstOrDefault(m => m.Id == matchId && m.Decision == MatchDecision.Accepted);
        if (match is not null)
        {
            match.FoodOffer ??= await unitOfWork.FoodOffers.GetByIdAsync(match.FoodOfferId, cancellationToken);
            match.CharityOrganization ??= await unitOfWork.CharityOrganizations.GetByIdAsync(match.CharityOrganizationId, cancellationToken);
            if (match.FoodOffer is not null)
            {
                match.FoodOffer.HospitalityPartner ??= await unitOfWork.HospitalityPartners.GetByIdAsync(match.FoodOffer.HospitalityPartnerId, cancellationToken);
            }
        }

        if (match?.FoodOffer?.HospitalityPartner is null || match.CharityOrganization is null)
        {
            return BadRequest(new { message = "Accepted match was not found." });
        }

        if (!User.IsAdministrator() && User.CharityOrganizationId() != match.CharityOrganizationId)
        {
            return Forbid();
        }

        // Fix: do not allow creating a pickup document if the hospitality partner
        // has been suspended since the match was created. A suspended partner cannot
        // fulfill the donation and the pickup would be unresolvable.
        if (match.FoodOffer.HospitalityPartner.ApprovalStatus == ApprovalStatus.Suspended)
        {
            return BadRequest(new { message = "Cannot create a pickup document because the hospitality partner account is currently suspended." });
        }

        if (match.FoodOffer.HospitalityPartner.ApprovalStatus != ApprovalStatus.Approved)
        {
            return BadRequest(new { message = "Cannot create a pickup document because the hospitality partner account is not approved." });
        }

        if (match.FoodOffer.Status is not (FoodOfferStatus.Reserved or FoodOfferStatus.Active or FoodOfferStatus.PublicFallback))
        {
            return BadRequest(new { message = "This offer is no longer available — it may have been cancelled, expired, or already completed." });
        }

        var existingPickup = unitOfWork.PickupDocuments.Query().FirstOrDefault(p => p.FoodOfferId == match.FoodOfferId && p.CharityOrganizationId == match.CharityOrganizationId);
        if (existingPickup is not null)
        {
            return BadRequest(new { message = "Pickup document already exists for this accepted match." });
        }

        var docSuffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var document = new PickupDocument
        {
            DocumentNumber = $"PR-{DateTime.UtcNow:yyyy}-{docSuffix}",
            FoodOfferId = match.FoodOfferId,
            HospitalityPartnerId = match.FoodOffer.HospitalityPartnerId,
            CharityOrganizationId = match.CharityOrganizationId,
            PlannedQuantityKg = match.FoodOffer.TotalQuantityKg,
            Status = PickupStatus.Created
        };

        await unitOfWork.PickupDocuments.AddAsync(document, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationPublisher.NotifyAsync(
            ActorType.HospitalityPartner,
            match.FoodOffer.HospitalityPartnerId,
            "Pickup document created",
            $"Organization \"{match.CharityOrganization.Name}\" created pickup {document.DocumentNumber}. A driver will be assigned shortly.",
            cancellationToken,
            NotificationType.PickupStatusChanged,
            ActorType.CharityOrganization,
            match.CharityOrganizationId);

        document.FoodOffer = match.FoodOffer;
        document.HospitalityPartner = match.FoodOffer.HospitalityPartner;
        document.CharityOrganization = match.CharityOrganization;
        if (document.FoodOffer.Items.Count == 0)
        {
            document.FoodOffer.Items = unitOfWork.FoodOfferItems.Query()
                .Where(i => i.FoodOfferId == document.FoodOfferId)
                .ToList();
        }

        return Ok(ToDto(document));
    }

    [Authorize(Roles = "CharityOrganization,Administrator")]
    [HttpPut("{id}/assign")]
    public async Task<IActionResult> Assign(int id, AssignPickupRequest request, CancellationToken cancellationToken)
    {
        var errors = assignValidator.Validate(request);
        if (!errors.IsValid)
        {
            return BadRequest(errors.Errors.Select(error => new { error.PropertyName, error.ErrorMessage }));
        }

        var pickup = await unitOfWork.PickupDocuments.GetByIdAsync(id, cancellationToken);
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(request.VehicleId, cancellationToken);
        if (pickup is null || vehicle is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.CharityOrganizationId() != pickup.CharityOrganizationId)
        {
            return Forbid();
        }

        // Fix: the organization must still be approved at the time of assignment.
        // It could have been suspended after the match was accepted.
        if (!User.IsAdministrator())
        {
            var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(pickup.CharityOrganizationId, cancellationToken);
            if (organization is null || organization.ApprovalStatus != ApprovalStatus.Approved)
            {
                return Forbid();
            }
        }

        if (pickup.Status != PickupStatus.Created)
        {
            return BadRequest(new { message = "Pickup can only be assigned while it is created." });
        }

        var driver = await unitOfWork.Drivers.GetByIdAsync(request.DriverId, cancellationToken);
        if (driver is null)
        {
            return NotFound();
        }

        if (driver.CharityOrganizationId != pickup.CharityOrganizationId || vehicle.CharityOrganizationId != pickup.CharityOrganizationId)
        {
            return BadRequest(new { message = "Driver and vehicle must belong to the pickup organization." });
        }

        if (!driver.IsActive)
        {
            return BadRequest(new { message = "Driver account is suspended and cannot be assigned to pickups." });
        }

        if (!driver.IsAvailable)
        {
            return BadRequest(new { message = "Driver is not available for new pickups." });
        }

        if (!vehicle.IsActive)
        {
            return BadRequest(new { message = "Vehicle is deactivated and cannot be assigned to pickups." });
        }

        if (!vehicle.IsAvailable)
        {
            return BadRequest(new { message = "Vehicle is not available for new pickups." });
        }

        if (vehicle.CapacityKg < pickup.PlannedQuantityKg)
        {
            return BadRequest(new { message = "Vehicle capacity is not sufficient for this pickup." });
        }

        pickup.DriverId = request.DriverId;
        pickup.VehicleId = request.VehicleId;
        pickup.Status = PickupStatus.Assigned;
        driver.IsAvailable = false;
        vehicle.IsAvailable = false;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var hospitalityPartner = await unitOfWork.HospitalityPartners.GetByIdAsync(pickup.HospitalityPartnerId, cancellationToken);
        var foodOffer = await unitOfWork.FoodOffers.GetByIdAsync(pickup.FoodOfferId, cancellationToken);
        var offerItems = foodOffer is not null
            ? unitOfWork.FoodOfferItems.Query()
                .Where(i => i.FoodOfferId == foodOffer.Id)
                .ToList()
            : [];

        var itemsList = offerItems.Count > 0
            ? string.Join(", ", offerItems.Select(i => $"{i.Name} ({i.Quantity} {i.Unit})"))
            : "nema detalja stavki";

        var pickupWindow = foodOffer is not null
            ? $"{foodOffer.PickupWindowStartUtc:HH:mm}–{foodOffer.PickupWindowEndUtc:HH:mm} UTC"
            : "nepoznat vremenski prozor";

        var partnerAddress = hospitalityPartner?.Address ?? "nepoznata adresa";
        var partnerName = hospitalityPartner?.Name ?? "nepoznat objekat";

        await notificationPublisher.NotifyAsync(
            ActorType.Driver,
            request.DriverId,
            "Dodeljeno preuzimanje",
            $"Preuzimanje {pickup.DocumentNumber} je dodeljeno vama. " +
            $"Objekat: {partnerName}, Adresa: {partnerAddress}, " +
            $"Vremenski prozor: {pickupWindow}, " +
            $"Hrana: {itemsList}.",
            cancellationToken,
            NotificationType.PickupAssigned);

        return NoContent();
    }

    [Authorize(Roles = "Driver,Administrator")]
    [HttpPut("{id}/driver-confirm")]
    public async Task<IActionResult> DriverConfirm(int id, CancellationToken cancellationToken)
    {
        var pickup = await unitOfWork.PickupDocuments.GetByIdAsync(id, cancellationToken);
        if (pickup is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.DriverId() != pickup.DriverId)
        {
            return Forbid();
        }

        if (pickup.Status != PickupStatus.Assigned)
        {
            return BadRequest(new { message = "Driver confirmation is only allowed for assigned pickups." });
        }

        pickup.Status = PickupStatus.DriverConfirmed;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "Driver,Administrator")]
    [HttpPut("{id}/complete")]
    public async Task<IActionResult> Complete(int id, CompletePickupRequest request, CancellationToken cancellationToken)
    {
        var validationResult = completeValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }));
        }

        var pickup = unitOfWork.PickupDocuments.Query().FirstOrDefault(p => p.Id == id);
        if (pickup is not null)
        {
            pickup.FoodOffer ??= await unitOfWork.FoodOffers.GetByIdAsync(pickup.FoodOfferId, cancellationToken);
            pickup.HospitalityPartner ??= await unitOfWork.HospitalityPartners.GetByIdAsync(pickup.HospitalityPartnerId, cancellationToken);
            pickup.CharityOrganization ??= await unitOfWork.CharityOrganizations.GetByIdAsync(pickup.CharityOrganizationId, cancellationToken);
        }
        if (pickup?.FoodOffer is null || pickup.HospitalityPartner is null || pickup.CharityOrganization is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.DriverId() != pickup.DriverId)
        {
            return Forbid();
        }

        if (pickup.Status != PickupStatus.DriverConfirmed)
        {
            return BadRequest(new { message = "Pickup can only be completed after driver confirmation." });
        }

        pickup.ActualQuantityKg = request.ActualQuantityKg;
        pickup.DriverNote = request.DriverNote;
        pickup.PickedUpAtUtc = DateTime.UtcNow;
        pickup.Status = PickupStatus.PickedUp;

        pickup.FoodOffer.Status = FoodOfferStatus.Completed;

        if (pickup.FoodOffer?.SpecialCampaignId is int specialCampaignId)
        {
            var campaign = await unitOfWork.SpecialCampaigns.GetByIdAsync(specialCampaignId, cancellationToken);
            if (campaign is not null)
            {
                campaign.CurrentQuantityKg += request.ActualQuantityKg;
                if (campaign.CurrentQuantityKg >= campaign.TargetQuantityKg)
                {
                    campaign.Status = CampaignStatus.Completed;
                }
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notificationPublisher.NotifyAsync(ActorType.HospitalityPartner, pickup.HospitalityPartnerId, "Food picked up", $"Pickup {pickup.DocumentNumber} was completed.", cancellationToken, NotificationType.PickupStatusChanged);
        await notificationPublisher.NotifyAsync(ActorType.CharityOrganization, pickup.CharityOrganizationId, "Food picked up", $"Pickup {pickup.DocumentNumber} was completed.", cancellationToken, NotificationType.PickupStatusChanged);
        return NoContent();
    }

    [Authorize(Roles = "Driver,Administrator")]
    [HttpPut("{id}/deliver")]
    public async Task<IActionResult> DeliverToOrganization(int id, CancellationToken cancellationToken)
    {
        var pickup = await unitOfWork.PickupDocuments.GetByIdAsync(id, cancellationToken);
        if (pickup is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.DriverId() != pickup.DriverId)
        {
            return Forbid();
        }

        if (pickup.Status == PickupStatus.DeliveredToOrganization)
        {
            return NoContent();
        }

        if (pickup.Status != PickupStatus.PickedUp)
        {
            return BadRequest(new { message = "Pickup can only be delivered after it has been picked up." });
        }

        pickup.Status = PickupStatus.DeliveredToOrganization;
        pickup.CharityOrganization ??= await unitOfWork.CharityOrganizations.GetByIdAsync(pickup.CharityOrganizationId, cancellationToken);
        if (pickup.CharityOrganization is not null)
        {
            pickup.CharityOrganization.LastReceivedAtUtc = DateTime.UtcNow;
            pickup.CharityOrganization.AcceptedMatchCount++;
            await unitOfWork.ReputationSnapshots.AddAsync(new ReputationSnapshot
            {
                ActorType = ActorType.CharityOrganization,
                ActorId = pickup.CharityOrganizationId,
                Score = pickup.CharityOrganization.ReputationScore,
                Source = "Delivery received"
            }, cancellationToken);
        }

        pickup.HospitalityPartner ??= await unitOfWork.HospitalityPartners.GetByIdAsync(pickup.HospitalityPartnerId, cancellationToken);
        if (pickup.HospitalityPartner is not null)
        {
            pickup.HospitalityPartner.TotalDonatedKg += pickup.ActualQuantityKg ?? pickup.PlannedQuantityKg;
            pickup.HospitalityPartner.SuccessfulDonations++;
        }

        if (pickup.DriverId.HasValue)
        {
            pickup.Driver ??= await unitOfWork.Drivers.GetByIdAsync(pickup.DriverId.Value, cancellationToken);
            if (pickup.Driver is not null)
            {
                pickup.Driver.IsAvailable = true;
                pickup.Driver.CompletedPickups++;
                await unitOfWork.ReputationSnapshots.AddAsync(new ReputationSnapshot
                {
                    ActorType = ActorType.Driver,
                    ActorId = pickup.DriverId.Value,
                    Score = pickup.Driver.ReputationScore,
                    Source = "Pickup delivered"
                }, cancellationToken);
            }
        }

        if (pickup.VehicleId.HasValue)
        {
            pickup.Vehicle ??= await unitOfWork.Vehicles.GetByIdAsync(pickup.VehicleId.Value, cancellationToken);
            if (pickup.Vehicle is not null)
            {
                pickup.Vehicle.IsAvailable = true;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notificationPublisher.NotifyAsync(ActorType.HospitalityPartner, pickup.HospitalityPartnerId, "Food delivered", $"Pickup {pickup.DocumentNumber} was delivered to the organization.", cancellationToken, NotificationType.PickupStatusChanged);
        await notificationPublisher.NotifyAsync(ActorType.CharityOrganization, pickup.CharityOrganizationId, "Food delivered", $"Pickup {pickup.DocumentNumber} was delivered.", cancellationToken, NotificationType.PickupStatusChanged);
        return NoContent();
    }

    [Authorize(Roles = "Driver,Administrator")]
    [HttpPut("{id}/driver-location")]
    public async Task<IActionResult> UpdateDriverLocation(int id, UpdateDriverLocationRequest request, CancellationToken cancellationToken)
    {
        var pickup = await unitOfWork.PickupDocuments.GetByIdAsync(id, cancellationToken);
        if (pickup is null)
        {
            return NotFound();
        }

        if (!pickup.DriverId.HasValue)
        {
            return BadRequest(new { message = "Pickup does not have an assigned driver." });
        }

        if (!User.IsAdministrator() && User.DriverId() != pickup.DriverId)
        {
            return Forbid();
        }

        if (pickup.Status is not (PickupStatus.Assigned or PickupStatus.DriverConfirmed or PickupStatus.PickedUp))
        {
            return BadRequest(new { message = "Driver location can only be updated for active pickups (assigned, confirmed, or in transit after pickup)." });
        }

        pickup.Driver ??= await unitOfWork.Drivers.GetByIdAsync(pickup.DriverId.Value, cancellationToken);
        if (pickup.Driver is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && pickup.Driver.CharityOrganizationId != pickup.CharityOrganizationId)
        {
            return Forbid();
        }

        pickup.Driver.CurrentLatitude = request.Latitude;
        pickup.Driver.CurrentLongitude = request.Longitude;
        pickup.Driver.LocationUpdatedAtUtc = DateTime.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var payload = new
        {
            pickupId = pickup.Id,
            pickup.DocumentNumber,
            driverId = pickup.DriverId.Value,
            latitude = request.Latitude,
            longitude = request.Longitude,
            updatedAtUtc = pickup.Driver.LocationUpdatedAtUtc
        };

        await hubContext.Clients.Group($"{ActorType.CharityOrganization}:{pickup.CharityOrganizationId}")
            .SendAsync("driver-location", payload, cancellationToken);
        await hubContext.Clients.Group($"{ActorType.Driver}:{pickup.DriverId.Value}")
            .SendAsync("driver-location", payload, cancellationToken);

        return NoContent();
    }

    [Authorize(Roles = "Driver,CharityOrganization,Administrator")]
    [HttpPost("{id}/issues")]
    public async Task<ActionResult<PickupIssue>> ReportIssue(int id, CreatePickupIssueRequest request, CancellationToken cancellationToken)
    {
        var pickup = await unitOfWork.PickupDocuments.GetByIdAsync(id, cancellationToken);
        if (pickup is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator()
            && User.DriverId() != pickup.DriverId
            && User.CharityOrganizationId() != pickup.CharityOrganizationId)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Note))
        {
            return BadRequest(new { message = "Issue note is required." });
        }

        // Fix: issues can only be reported on pickups that are still in progress.
        // Reporting on a completed, cancelled, or already-problematic pickup makes no
        // operational sense and would create confusing data in the system.
        if (NonReportableStatuses.Contains(pickup.Status))
        {
            return BadRequest(new
            {
                message = pickup.Status switch
                {
                    PickupStatus.PickedUp => "Cannot report an issue on a pickup that has already been completed (food picked up). Use the deliver endpoint or cancel if needed.",
                    PickupStatus.DeliveredToOrganization => "Cannot report an issue on a pickup that has already been delivered.",
                    PickupStatus.Cancelled => "Cannot report an issue on a cancelled pickup.",
                    PickupStatus.ProblemReported => "This pickup already has a reported problem. Resolve the existing issue first via PUT /pickups/{id}/resolve-issue.",
                    _ => "Cannot report an issue on this pickup in its current state."
                }
            });
        }

        var issue = new PickupIssue
        {
            PickupDocumentId = id,
            IssueType = request.IssueType,
            Note = request.Note
        };

        pickup.Status = PickupStatus.ProblemReported;

        if (request.IssueType == PickupIssueType.FoodUnavailable)
        {
            var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(pickup.HospitalityPartnerId, cancellationToken);
            if (partner is not null)
            {
                var previousCancellationCount = partner.CancellationCount;
                partner.CancellationCount++;
                partner.ReputationScore = Math.Max(1, Math.Round(partner.ReputationScore - 0.3, 2));
                await unitOfWork.ReputationSnapshots.AddAsync(new ReputationSnapshot
                {
                    ActorType = ActorType.HospitalityPartner,
                    ActorId = partner.Id,
                    Score = partner.ReputationScore,
                    Source = "Food unavailable"
                }, cancellationToken);

                if (previousCancellationCount < 3 && partner.CancellationCount >= 3)
                {
                    await notificationPublisher.NotifyAsync(
                        ActorType.Administrator,
                        0,
                        "Cancellation warning",
                        $"Hospitality partner #{partner.Id} has reached {partner.CancellationCount} cancellations.",
                        cancellationToken,
                        NotificationType.AdminMessage);
                }
            }

            var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(pickup.CharityOrganizationId, cancellationToken);
            if (organization is not null)
            {
                organization.LastReceivedAtUtc = null;
                organization.MatchCompensationBonus = Math.Max(organization.MatchCompensationBonus, 3m);
                organization.MatchCompensationExpiresAtUtc = DateTime.UtcNow.AddDays(3);
            }

            var offer = await unitOfWork.FoodOffers.GetByIdAsync(pickup.FoodOfferId, cancellationToken);
            if (offer is not null)
            {
                offer.Status = FoodOfferStatus.PublicFallback;
            }
        }
        else if (request.IssueType is PickupIssueType.DriverUnavailable or PickupIssueType.LateArrival)
        {
            var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(pickup.CharityOrganizationId, cancellationToken);
            if (organization is not null)
            {
                var previousCancellationCount = organization.CancellationCount;
                organization.CancellationCount++;
                organization.ReputationScore = Math.Max(1, Math.Round(organization.ReputationScore - 0.2, 2));

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

            if (request.IssueType == PickupIssueType.DriverUnavailable && pickup.DriverId.HasValue)
            {
                var unavailableDriver = await unitOfWork.Drivers.GetByIdAsync(pickup.DriverId.Value, cancellationToken);
                if (unavailableDriver is not null)
                {
                    unavailableDriver.CancellationCount++;
                    unavailableDriver.ReputationScore = Math.Max(1, Math.Round(unavailableDriver.ReputationScore - 0.2, 2));
                    unavailableDriver.IsAvailable = true;
                    await unitOfWork.ReputationSnapshots.AddAsync(new ReputationSnapshot
                    {
                        ActorType = ActorType.Driver,
                        ActorId = unavailableDriver.Id,
                        Score = unavailableDriver.ReputationScore,
                        Source = "Driver unavailable"
                    }, cancellationToken);
                }

                if (pickup.VehicleId.HasValue)
                {
                    var releasedVehicle = await unitOfWork.Vehicles.GetByIdAsync(pickup.VehicleId.Value, cancellationToken);
                    if (releasedVehicle is not null)
                    {
                        releasedVehicle.IsAvailable = true;
                    }
                }
            }
        }
        else if (request.IssueType == PickupIssueType.QuantityMismatch)
        {
            var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(pickup.HospitalityPartnerId, cancellationToken);
            if (partner is not null)
            {
                partner.ReputationScore = Math.Max(1, Math.Round(partner.ReputationScore - 0.1, 2));
                await unitOfWork.ReputationSnapshots.AddAsync(new ReputationSnapshot
                {
                    ActorType = ActorType.HospitalityPartner,
                    ActorId = partner.Id,
                    Score = partner.ReputationScore,
                    Source = "Quantity mismatch"
                }, cancellationToken);
            }

            await notificationPublisher.NotifyAsync(
                ActorType.Administrator,
                0,
                "Quantity mismatch reported",
                $"Driver reported a quantity mismatch on pickup {pickup.DocumentNumber}. Note: {request.Note}",
                cancellationToken,
                NotificationType.AdminMessage);
        }
        else if (request.IssueType == PickupIssueType.Other)
        {
            await notificationPublisher.NotifyAsync(
                ActorType.Administrator,
                0,
                "Pickup issue reported",
                $"A general issue was reported on pickup {pickup.DocumentNumber}. Note: {request.Note}",
                cancellationToken,
                NotificationType.AdminMessage);
        }

        await unitOfWork.PickupIssues.AddAsync(issue, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notificationPublisher.NotifyAsync(ActorType.Administrator, 0, "Pickup issue reported", $"Issue reported for pickup {pickup.DocumentNumber}: {request.Note}", cancellationToken, NotificationType.PickupStatusChanged);
        return Ok(issue);
    }

    [Authorize(Roles = "CharityOrganization,Administrator")]
    [HttpPut("{id}/resolve-issue")]
    public async Task<IActionResult> ResolveIssue(int id, ResolvePickupIssueRequest request, CancellationToken cancellationToken)
    {
        var pickup = await unitOfWork.PickupDocuments.GetByIdAsync(id, cancellationToken);
        if (pickup is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.CharityOrganizationId() != pickup.CharityOrganizationId)
        {
            return Forbid();
        }

        if (pickup.Status != PickupStatus.ProblemReported)
        {
            return BadRequest(new { message = "Only pickups with a reported problem can be resolved." });
        }

        if (request.Cancel)
        {
            pickup.Status = PickupStatus.Cancelled;
            pickup.CancellationReason ??= "Pickup cancelled after issue resolution.";
            pickup.CancelledAtUtc = DateTime.UtcNow;

            if (pickup.DriverId.HasValue)
            {
                pickup.Driver ??= await unitOfWork.Drivers.GetByIdAsync(pickup.DriverId.Value, cancellationToken);
                if (pickup.Driver is not null)
                {
                    pickup.Driver.IsAvailable = true;
                }
            }

            if (pickup.VehicleId.HasValue)
            {
                pickup.Vehicle ??= await unitOfWork.Vehicles.GetByIdAsync(pickup.VehicleId.Value, cancellationToken);
                if (pickup.Vehicle is not null)
                {
                    pickup.Vehicle.IsAvailable = true;
                }
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await notificationPublisher.NotifyAsync(
                ActorType.HospitalityPartner,
                pickup.HospitalityPartnerId,
                "Pickup cancelled after issue",
                $"Pickup {pickup.DocumentNumber} was cancelled after issue resolution.",
                cancellationToken,
                NotificationType.Cancellation);
        }
        else
        {
            pickup.Status = pickup.DriverId.HasValue ? PickupStatus.DriverConfirmed : PickupStatus.Created;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await notificationPublisher.NotifyAsync(
                ActorType.HospitalityPartner,
                pickup.HospitalityPartnerId,
                "Pickup issue resolved",
                $"The issue on pickup {pickup.DocumentNumber} has been resolved and the pickup is resuming.",
                cancellationToken,
                NotificationType.PickupStatusChanged);

            if (pickup.DriverId.HasValue)
            {
                await notificationPublisher.NotifyAsync(
                    ActorType.Driver,
                    pickup.DriverId.Value,
                    "Pickup resumed",
                    $"The issue on pickup {pickup.DocumentNumber} has been resolved. Please continue.",
                    cancellationToken,
                    NotificationType.PickupStatusChanged);
            }
        }

        return NoContent();
    }

    [Authorize(Roles = "CharityOrganization,Administrator")]
    [HttpPut("{id}/cancel-by-organization")]
    public async Task<IActionResult> CancelByOrganization(int id, CancelPickupRequest request, CancellationToken cancellationToken)
    {
        var pickup = await unitOfWork.PickupDocuments.GetByIdAsync(id, cancellationToken);
        if (pickup is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.CharityOrganizationId() != pickup.CharityOrganizationId)
        {
            return Forbid();
        }

        if (pickup.Status is PickupStatus.PickedUp
                          or PickupStatus.DeliveredToOrganization
                          or PickupStatus.Cancelled
                          or PickupStatus.ProblemReported)
        {
            return BadRequest(new
            {
                message = pickup.Status == PickupStatus.ProblemReported
                    ? "Pickups with a reported problem must be resolved via PUT /pickups/{id}/resolve-issue."
                    : "This pickup cannot be cancelled by the organization anymore."
            });
        }

        var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(pickup.CharityOrganizationId, cancellationToken);
        var offer = await unitOfWork.FoodOffers.GetByIdAsync(pickup.FoodOfferId, cancellationToken);
        var nextPending = unitOfWork.OfferMatches.Query()
            .Where(m => m.FoodOfferId == pickup.FoodOfferId && m.Decision == MatchDecision.Pending && m.CharityOrganizationId != pickup.CharityOrganizationId)
            .OrderBy(m => m.Rank)
            .FirstOrDefault();

        pickup.Status = PickupStatus.Cancelled;
        pickup.CancellationReason = request.Reason;
        pickup.CancelledAtUtc = DateTime.UtcNow;

        if (organization is not null)
        {
            var previousCancellationCount = organization.CancellationCount;
            organization.CancellationCount++;
            organization.ReputationScore = Math.Max(1, Math.Round(organization.ReputationScore - 0.2, 2));

            await unitOfWork.ReputationSnapshots.AddAsync(new ReputationSnapshot
            {
                ActorType = ActorType.CharityOrganization,
                ActorId = organization.Id,
                Score = organization.ReputationScore,
                Source = "Pickup cancellation"
            }, cancellationToken);

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

        if (offer is not null)
        {
            offer.Status = nextPending is null ? FoodOfferStatus.PublicFallback : FoodOfferStatus.Active;
        }

        if (pickup.DriverId.HasValue)
        {
            var assignedDriver = await unitOfWork.Drivers.GetByIdAsync(pickup.DriverId.Value, cancellationToken);
            if (assignedDriver is not null)
            {
                assignedDriver.IsAvailable = true;
            }
        }

        if (pickup.VehicleId.HasValue)
        {
            var assignedVehicle = await unitOfWork.Vehicles.GetByIdAsync(pickup.VehicleId.Value, cancellationToken);
            if (assignedVehicle is not null)
            {
                assignedVehicle.IsAvailable = true;
            }
        }

        await unitOfWork.PlatformMessages.AddAsync(new PlatformMessage
        {
            SenderType = ActorType.CharityOrganization,
            SenderId = pickup.CharityOrganizationId,
            RecipientType = ActorType.HospitalityPartner,
            RecipientId = pickup.HospitalityPartnerId,
            PickupDocumentId = pickup.Id,
            FoodOfferId = pickup.FoodOfferId,
            MessageType = MessageType.System,
            Body = $"Pickup was cancelled by organization. Reason: {request.Reason}"
        }, cancellationToken);

        if (nextPending is not null)
        {
            nextPending.NotifiedAtUtc = DateTime.UtcNow;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notificationPublisher.NotifyAsync(ActorType.HospitalityPartner, pickup.HospitalityPartnerId, "Pickup cancelled", request.Reason, cancellationToken, NotificationType.Cancellation);

        if (nextPending is not null)
        {
            await notificationPublisher.NotifyAsync(
                ActorType.CharityOrganization,
                nextPending.CharityOrganizationId,
                "Pickup reopened",
                $"Pickup {pickup.DocumentNumber} became available again.",
                cancellationToken,
                NotificationType.MatchingOffer);
        }
        return NoContent();
    }

    private bool CanAccessPickup(PickupDocument pickup)
    {
        return User.IsAdministrator()
            || User.HospitalityPartnerId() == pickup.HospitalityPartnerId
            || User.CharityOrganizationId() == pickup.CharityOrganizationId
            || User.DriverId() == pickup.DriverId;
    }

    private async Task LoadPickupDetailsAsync(PickupDocument pickup, CancellationToken cancellationToken)
    {
        pickup.HospitalityPartner ??= await unitOfWork.HospitalityPartners.GetByIdAsync(pickup.HospitalityPartnerId, cancellationToken);
        pickup.CharityOrganization ??= await unitOfWork.CharityOrganizations.GetByIdAsync(pickup.CharityOrganizationId, cancellationToken);

        pickup.FoodOffer ??= await unitOfWork.FoodOffers.GetByIdAsync(pickup.FoodOfferId, cancellationToken);
        if (pickup.FoodOffer is not null && pickup.FoodOffer.Items.Count == 0)
        {
            pickup.FoodOffer.Items = unitOfWork.FoodOfferItems.Query()
                .Where(i => i.FoodOfferId == pickup.FoodOffer.Id)
                .ToList();
        }

        if (pickup.DriverId.HasValue)
        {
            pickup.Driver ??= await unitOfWork.Drivers.GetByIdAsync(pickup.DriverId.Value, cancellationToken);
        }

        if (pickup.VehicleId.HasValue)
        {
            pickup.Vehicle ??= await unitOfWork.Vehicles.GetByIdAsync(pickup.VehicleId.Value, cancellationToken);
        }
    }

    private static PickupDocumentDto ToDto(PickupDocument pickup)
    {
        return new PickupDocumentDto
        {
            Id = pickup.Id,
            DocumentNumber = pickup.DocumentNumber,
            PartnerName = pickup.HospitalityPartner?.Name ?? string.Empty,
            OrganizationName = pickup.CharityOrganization?.Name ?? string.Empty,
            Status = pickup.Status,
            PlannedQuantityKg = pickup.PlannedQuantityKg,
            ActualQuantityKg = pickup.ActualQuantityKg,
            PickedUpAtUtc = pickup.PickedUpAtUtc,
            DriverName = pickup.Driver?.FullName,
            VehicleRegistrationNumber = pickup.Vehicle?.RegistrationNumber,
            DriverLatitude = pickup.Driver?.CurrentLatitude,
            DriverLongitude = pickup.Driver?.CurrentLongitude,
            DriverLocationUpdatedAtUtc = pickup.Driver?.LocationUpdatedAtUtc,
            Items = pickup.FoodOffer?.Items.Select(i => new PickupItemDto
            {
                Name = i.Name,
                Quantity = i.Quantity,
                Unit = i.Unit,
                DietaryTags = i.DietaryTags
            }).ToList() ?? []
        };
    }
}
