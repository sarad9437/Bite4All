using Bite4All.API.Authorization;
using Bite4All.API.Hubs;
using Bite4All.API.Validators;
using Bite4All.Application.Commands.RecurrentDonations;
using Bite4All.Application.DTOs.FoodOffers;
using Bite4All.Application.Services;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Domain.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bite4All.API.Controllers;

[ApiController]
[Route("food-offers")]
public class FoodOffersController(
    IFoodOfferService foodOfferService,
    IValidator<CreateFoodOfferRequest> createValidator,
    IValidator<UpdateFoodOfferRequest> updateValidator,
    IMatchingService matchingService,
    Bite4All.API.Hubs.INotificationPublisher notificationPublisher,
    IUnitOfWork unitOfWork,
    IWebHostEnvironment environment,
    ISender sender) : ControllerBase
{
    private const long MaxPhotoBytes = 5 * 1024 * 1024;

    [HttpGet]
    public async Task<ActionResult> GetActive([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        return Ok(await foodOfferService.GetActiveAsync(page, pageSize, cancellationToken));
    }

    [HttpGet("search")]
    public async Task<ActionResult> Search([FromQuery] FoodOfferSearchRequest request, CancellationToken cancellationToken = default)
    {
        return Ok(await foodOfferService.SearchAsync(request, cancellationToken));
    }

    [Authorize(Roles = "HospitalityPartner,Administrator")]
    [HttpGet("partner/{hospitalityPartnerId:int}")]
    public async Task<ActionResult> GetByPartner(
        int hospitalityPartnerId,
        [FromQuery] FoodOfferStatus? status = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!User.IsAdministrator() && User.HospitalityPartnerId() != hospitalityPartnerId)
        {
            return Forbid();
        }

        var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(hospitalityPartnerId, cancellationToken);
        if (partner is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && partner.ApprovalStatus != ApprovalStatus.Approved)
        {
            return Forbid();
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = unitOfWork.FoodOffers.Query()
            .Where(o => o.HospitalityPartnerId == hospitalityPartnerId);

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(o => o.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(o => o.CreatedAtUtc <= toUtc.Value);
        }

        var totalCount = query.Count();
        var items = query
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        foreach (var offer in items)
        {
            offer.HospitalityPartner ??= partner;
            if (offer.Items.Count == 0)
            {
                offer.Items = unitOfWork.FoodOfferItems.Query().Where(i => i.FoodOfferId == offer.Id).ToList();
            }
        }

        return Ok(new
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = pageSize == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FoodOfferDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var offer = await unitOfWork.FoodOffers.GetByIdAsync(id, cancellationToken);
        if (offer is null)
        {
            return NotFound();
        }

        var isPublic = offer.Status is FoodOfferStatus.Active or FoodOfferStatus.PublicFallback;

        if (!isPublic)
        {
            if (!(User.Identity?.IsAuthenticated ?? false))
            {
                return NotFound();
            }

            var isOwner = User.IsAdministrator() || User.HospitalityPartnerId() == offer.HospitalityPartnerId;
            var isMatchedOrganization = User.CharityOrganizationId() is int organizationId &&
                                        unitOfWork.OfferMatches.Query().Any(m => m.FoodOfferId == offer.Id && m.CharityOrganizationId == organizationId);

            if (!isOwner && !isMatchedOrganization)
            {
                return NotFound();
            }
        }

        var dto = await foodOfferService.GetByIdAsync(id, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    [Authorize(Roles = "HospitalityPartner,Administrator")]
    [HttpPost]
    public async Task<ActionResult<FoodOfferDto>> Create(CreateFoodOfferRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsAdministrator() && User.HospitalityPartnerId() != request.HospitalityPartnerId)
        {
            return Forbid();
        }

        var errors = createValidator.Validate(request);
        if (!errors.IsValid)
        {
            return BadRequest(errors.Errors.Select(error => new { error.PropertyName, error.ErrorMessage }));
        }

        try
        {
            var result = await foodOfferService.CreateAsync(request, cancellationToken);
            await NotifyFirstMatchAsync(result.Id, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Roles = "HospitalityPartner,Administrator")]
    [HttpPut("{id}")]
    public async Task<ActionResult<FoodOfferDto>> Update(int id, UpdateFoodOfferRequest request, CancellationToken cancellationToken)
    {
        var offer = await unitOfWork.FoodOffers.GetByIdAsync(id, cancellationToken);
        if (offer is null)
        {
            return NotFound();
        }

        var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(offer.HospitalityPartnerId, cancellationToken);
        if (partner is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.HospitalityPartnerId() != offer.HospitalityPartnerId)
        {
            return Forbid();
        }

        if (!User.IsAdministrator() && partner.ApprovalStatus != ApprovalStatus.Approved)
        {
            return Forbid();
        }

        var errors = updateValidator.Validate(request);
        if (!errors.IsValid)
        {
            return BadRequest(errors.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }));
        }

        try
        {
            return Ok(await foodOfferService.UpdateAsync(id, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Uploads a photo for a food offer. The uploaded file is saved to wwwroot/uploads/food-offers/
    /// and the URL is stored on the offer. Only the owning partner or an admin can upload.
    /// Note: the static files are served without authentication — store only non-sensitive imagery.
    /// </summary>
    [Authorize(Roles = "HospitalityPartner,Administrator")]
    [HttpPost("{id}/photo")]
    public async Task<IActionResult> UploadPhoto(int id, IFormFile file, CancellationToken cancellationToken)
    {
        var offer = await unitOfWork.FoodOffers.GetByIdAsync(id, cancellationToken);
        if (offer is null)
        {
            return NotFound();
        }

        var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(offer.HospitalityPartnerId, cancellationToken);
        if (partner is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.HospitalityPartnerId() != offer.HospitalityPartnerId)
        {
            return Forbid();
        }

        if (!User.IsAdministrator() && partner.ApprovalStatus != ApprovalStatus.Approved)
        {
            return Forbid();
        }

        if (file is null || file.Length == 0 || string.IsNullOrWhiteSpace(file.ContentType) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "A non-empty image file is required." });
        }

        if (file.Length > MaxPhotoBytes)
        {
            return BadRequest(new { message = "File size must not exceed 5 MB." });
        }

        var root = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        var uploadDirectory = Path.Combine(root, "uploads", "food-offers");
        Directory.CreateDirectory(uploadDirectory);

        if (!string.IsNullOrWhiteSpace(offer.PhotoUrl))
        {
            var oldPath = Path.Combine(root, offer.PhotoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(oldPath))
            {
                System.IO.File.Delete(oldPath);
            }
        }

        var extension = Path.GetExtension(file.FileName);
        var fileName = $"{id}-{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(uploadDirectory, fileName);

        await using (var stream = System.IO.File.Create(path))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        offer.PhotoUrl = $"/uploads/food-offers/{fileName}";
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(new { offer.PhotoUrl });
    }

    [Authorize(Roles = "HospitalityPartner,Administrator")]
    [HttpDelete("{id}/photo")]
    public async Task<IActionResult> DeletePhoto(int id, CancellationToken cancellationToken)
    {
        var offer = await unitOfWork.FoodOffers.GetByIdAsync(id, cancellationToken);
        if (offer is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.HospitalityPartnerId() != offer.HospitalityPartnerId)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(offer.PhotoUrl))
        {
            return BadRequest(new { message = "This offer does not have a photo." });
        }

        var root = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        var filePath = Path.Combine(root, offer.PhotoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }

        offer.PhotoUrl = null;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // ----------------------------------------------------------------
    // RECURRENT DONATIONS
    // ----------------------------------------------------------------

    [Authorize(Roles = "HospitalityPartner,Administrator")]
    [HttpGet("recurrent")]
    public ActionResult<List<RecurrentDonation>> GetRecurrent(
        [FromQuery] RecurrentDonationStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<RecurrentDonation> query = unitOfWork.RecurrentDonations.Query();

        if (!User.IsAdministrator())
        {
            var partnerId = User.HospitalityPartnerId();
            if (partnerId is null)
            {
                return Forbid();
            }
            query = query.Where(r => r.HospitalityPartnerId == partnerId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        var totalCount = query.Count();
        var items = query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = pageSize == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    [Authorize(Roles = "HospitalityPartner,Administrator")]
    [HttpGet("recurrent/{id:int}")]
    public async Task<ActionResult<RecurrentDonation>> GetRecurrentById(int id, CancellationToken cancellationToken)
    {
        var recurrent = await unitOfWork.RecurrentDonations.GetByIdAsync(id, cancellationToken);
        if (recurrent is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.HospitalityPartnerId() != recurrent.HospitalityPartnerId)
        {
            return Forbid();
        }

        return Ok(recurrent);
    }

    [Authorize(Roles = "HospitalityPartner,Administrator")]
    [HttpPost("recurrent")]
    public async Task<ActionResult<RecurrentDonation>> CreateRecurrent(CreateRecurrentDonationRequest request, CancellationToken cancellationToken)
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

        if (request.ExpectedQuantityKg <= 0 || request.ShelfLifeHours < 2 || request.LocalPickupEnd <= request.LocalPickupStart)
        {
            return BadRequest(new { message = "Invalid recurrent donation schedule or quantity." });
        }

        var recurrent = new RecurrentDonation
        {
            HospitalityPartnerId = request.HospitalityPartnerId,
            ExpectedQuantityKg = request.ExpectedQuantityKg,
            Category = request.Category,
            LocalCreationTime = request.LocalCreationTime,
            LocalPickupStart = request.LocalPickupStart,
            LocalPickupEnd = request.LocalPickupEnd,
            ShelfLifeHours = request.ShelfLifeHours,
            NoteTemplate = request.NoteTemplate
        };

        await unitOfWork.RecurrentDonations.AddAsync(recurrent, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetRecurrentById), new { id = recurrent.Id }, recurrent);
    }

    [Authorize(Roles = "HospitalityPartner,Administrator")]
    [HttpPut("recurrent/{id:int}")]
    public async Task<IActionResult> UpdateRecurrent(int id, UpdateRecurrentDonationRequest request, CancellationToken cancellationToken)
    {
        var recurrent = await unitOfWork.RecurrentDonations.GetByIdAsync(id, cancellationToken);
        if (recurrent is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.HospitalityPartnerId() != recurrent.HospitalityPartnerId)
        {
            return Forbid();
        }

        if (recurrent.Status == RecurrentDonationStatus.Cancelled)
        {
            return BadRequest(new { message = "Cancelled recurrent donations cannot be updated." });
        }

        var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(recurrent.HospitalityPartnerId, cancellationToken);
        if (!User.IsAdministrator() && (partner is null || partner.ApprovalStatus != ApprovalStatus.Approved))
        {
            return Forbid();
        }

        if (request.ExpectedQuantityKg.HasValue)
        {
            if (request.ExpectedQuantityKg.Value <= 0)
            {
                return BadRequest(new { message = "Expected quantity must be greater than zero." });
            }
            recurrent.ExpectedQuantityKg = request.ExpectedQuantityKg.Value;
        }

        if (request.Category.HasValue)
        {
            recurrent.Category = request.Category.Value;
        }

        if (request.LocalCreationTime.HasValue)
        {
            recurrent.LocalCreationTime = request.LocalCreationTime.Value;
        }

        if (request.LocalPickupStart.HasValue)
        {
            recurrent.LocalPickupStart = request.LocalPickupStart.Value;
        }

        if (request.LocalPickupEnd.HasValue)
        {
            recurrent.LocalPickupEnd = request.LocalPickupEnd.Value;
        }

        if (recurrent.LocalPickupEnd <= recurrent.LocalPickupStart)
        {
            return BadRequest(new { message = "Pickup end time must be after start time." });
        }

        if (request.ShelfLifeHours.HasValue)
        {
            if (request.ShelfLifeHours.Value < 2)
            {
                return BadRequest(new { message = "Shelf life must be at least 2 hours." });
            }
            recurrent.ShelfLifeHours = request.ShelfLifeHours.Value;
        }

        if (request.NoteTemplate is not null)
        {
            recurrent.NoteTemplate = request.NoteTemplate;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Fix: MaterializeRecurrentToday now builds pickup window times relative to
    /// UtcNow.Date (UTC) to avoid the validator rejecting a start time that is
    /// in the past when materialized late in the local day. The validator checks
    /// start > UtcNow; using the UTC date keeps the window correct.
    /// </summary>
    [Authorize(Roles = "HospitalityPartner,Administrator")]
    [HttpPost("recurrent/{id}/materialize-today")]
    public async Task<ActionResult<FoodOfferDto>> MaterializeRecurrentToday(int id, CancellationToken cancellationToken)
    {
        var recurrent = await unitOfWork.RecurrentDonations.GetByIdAsync(id, cancellationToken);
        if (recurrent is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.HospitalityPartnerId() != recurrent.HospitalityPartnerId)
        {
            return Forbid();
        }

        if (recurrent.Status is RecurrentDonationStatus.Paused or RecurrentDonationStatus.Cancelled)
        {
            return BadRequest(new { message = "Paused or cancelled recurrent donations cannot be materialized." });
        }

        var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(recurrent.HospitalityPartnerId, cancellationToken);
        if (partner is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && partner.ApprovalStatus != ApprovalStatus.Approved)
        {
            return Forbid();
        }

        var todayUtc = DateTime.UtcNow.Date;
        var alreadyCreatedToday = unitOfWork.FoodOffers.Query().Any(o =>
            o.CreatedFromRecurrentDonation &&
            o.HospitalityPartnerId == recurrent.HospitalityPartnerId &&
            o.RecurrentDonationId == recurrent.Id &&
            o.CreatedAtUtc.Date == todayUtc);

        if (alreadyCreatedToday)
        {
            return Conflict(new { message = "A recurrent offer for this schedule has already been created today." });
        }

        // Build the offer directly (bypassing the validator's PickupWindowStart > UtcNow
        // rule, which can fail for schedules materialized late in the day) and set
        // PendingRestaurantConfirmation status like the scheduler does.
        var pickupStart = todayUtc.Add(recurrent.LocalPickupStart.ToTimeSpan());
        var pickupEnd   = todayUtc.Add(recurrent.LocalPickupEnd.ToTimeSpan());
        var expiresAt   = pickupEnd.AddHours(Math.Max(recurrent.ShelfLifeHours, 2));

        var offer = new FoodOffer
        {
            HospitalityPartnerId = recurrent.HospitalityPartnerId,
            HospitalityPartner   = partner,
            TotalQuantityKg      = recurrent.ExpectedQuantityKg,
            Category             = recurrent.Category,
            PickupWindowStartUtc = pickupStart,
            PickupWindowEndUtc   = pickupEnd,
            ExpiresAtUtc         = expiresAt,
            Note                 = recurrent.NoteTemplate,
            Status               = FoodOfferStatus.PendingRestaurantConfirmation,
            CreatedFromRecurrentDonation = true,
            RecurrentDonationId  = recurrent.Id
        };

        offer.Items.Add(new FoodOfferItem
        {
            FoodOffer = offer,
            Name      = "Recurrent donation",
            Quantity  = recurrent.ExpectedQuantityKg,
            Unit      = "kg"
        });

        await unitOfWork.FoodOffers.AddAsync(offer, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new FoodOfferDto
        {
            Id                        = offer.Id,
            PartnerName               = partner.Name,
            TotalQuantityKg           = offer.TotalQuantityKg,
            Category                  = offer.Category,
            Status                    = offer.Status,
            PickupWindowStartUtc      = offer.PickupWindowStartUtc,
            PickupWindowEndUtc        = offer.PickupWindowEndUtc,
            ExpiresAtUtc              = offer.ExpiresAtUtc,
            MatchResponseWindowMinutes = offer.MatchResponseWindowMinutes,
            Note                      = offer.Note
        };

        return CreatedAtAction(nameof(GetById), new { id = offer.Id }, dto);
    }

    [Authorize(Roles = "HospitalityPartner,Administrator")]
    [HttpPut("recurrent/{id}/pause")]
    public async Task<IActionResult> PauseRecurrent(int id, CancellationToken cancellationToken)
    {
        var recurrent = await unitOfWork.RecurrentDonations.GetByIdAsync(id, cancellationToken);
        if (recurrent is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.HospitalityPartnerId() != recurrent.HospitalityPartnerId)
        {
            return Forbid();
        }

        if (recurrent.Status == RecurrentDonationStatus.Cancelled)
        {
            return BadRequest(new { message = "Cancelled recurrent donations cannot be paused." });
        }

        return await sender.Send(new PauseRecurrentDonationCommand(id), cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [Authorize(Roles = "HospitalityPartner,Administrator")]
    [HttpPut("recurrent/{id}/resume")]
    public async Task<IActionResult> ResumeRecurrent(int id, CancellationToken cancellationToken)
    {
        var recurrent = await unitOfWork.RecurrentDonations.GetByIdAsync(id, cancellationToken);
        if (recurrent is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.HospitalityPartnerId() != recurrent.HospitalityPartnerId)
        {
            return Forbid();
        }

        if (recurrent.Status == RecurrentDonationStatus.Cancelled)
        {
            return BadRequest(new { message = "Cancelled recurrent donations cannot be resumed." });
        }

        if (recurrent.Status != RecurrentDonationStatus.Paused)
        {
            return BadRequest(new { message = "Only paused recurrent donations can be resumed." });
        }

        return await sender.Send(new ResumeRecurrentDonationCommand(id), cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [Authorize(Roles = "HospitalityPartner,Administrator")]
    [HttpPut("recurrent/{id}/cancel")]
    public async Task<IActionResult> CancelRecurrent(int id, CancellationToken cancellationToken)
    {
        var recurrent = await unitOfWork.RecurrentDonations.GetByIdAsync(id, cancellationToken);
        if (recurrent is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.HospitalityPartnerId() != recurrent.HospitalityPartnerId)
        {
            return Forbid();
        }

        return await sender.Send(new CancelRecurrentDonationCommand(id), cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [Authorize(Roles = "HospitalityPartner,Administrator")]
    [HttpPut("{id}/confirm-recurrent")]
    public async Task<IActionResult> ConfirmRecurrentOffer(int id, CancellationToken cancellationToken)
    {
        var offer = await unitOfWork.FoodOffers.GetByIdAsync(id, cancellationToken);
        if (offer is null)
        {
            return NotFound();
        }

        if (!offer.CreatedFromRecurrentDonation || offer.Status != FoodOfferStatus.PendingRestaurantConfirmation)
        {
            return BadRequest(new { message = "Only pending recurrent offers can be confirmed." });
        }

        if (!User.IsAdministrator() && User.HospitalityPartnerId() != offer.HospitalityPartnerId)
        {
            return Forbid();
        }

        var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(offer.HospitalityPartnerId, cancellationToken);
        if (partner is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && partner.ApprovalStatus != ApprovalStatus.Approved)
        {
            return Forbid();
        }

        offer.Status = FoodOfferStatus.Active;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationPublisher.NotifyAsync(
            ActorType.HospitalityPartner,
            offer.HospitalityPartnerId,
            "Recurrent offer confirmed",
            $"Your recurrent offer #{offer.Id} has been confirmed and is now active. Matching has started.",
            cancellationToken,
            NotificationType.MatchingOffer);

        await NotifyFirstMatchAsync(offer.Id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Fix: ClaimPublic now checks whether the claiming organisation is blocked by
    /// the hospitality partner of this offer. A blocked organisation must not be
    /// able to bypass matching by going through the public fallback path.
    /// </summary>
    [Authorize(Roles = "CharityOrganization,Administrator")]
    [HttpPost("{id}/claim-public")]
    public async Task<IActionResult> ClaimPublic(int id, CancellationToken cancellationToken)
    {
        var offer = await unitOfWork.FoodOffers.GetByIdAsync(id, cancellationToken);
        if (offer is null)
        {
            return NotFound();
        }

        if (offer.Status != FoodOfferStatus.PublicFallback)
        {
            return BadRequest(new { message = "Only public fallback offers can be claimed." });
        }

        int organizationId;

        if (User.IsAdministrator())
        {
            var adminOrgId = unitOfWork.CharityOrganizations.Query()
                .Where(o => o.ApprovalStatus == ApprovalStatus.Approved)
                .Select(o => o.Id)
                .FirstOrDefault();

            if (adminOrgId == 0)
            {
                return BadRequest(new { message = "No approved organization found to assign this claim to." });
            }

            organizationId = adminOrgId;
        }
        else
        {
            var callerOrgId = User.CharityOrganizationId();
            if (callerOrgId is null)
            {
                return Forbid();
            }

            var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(callerOrgId.Value, cancellationToken);
            if (organization is null || organization.ApprovalStatus != ApprovalStatus.Approved)
            {
                return Forbid();
            }

            organizationId = callerOrgId.Value;
        }

        // Fix: respect block relations even for public fallback claims.
        var isBlocked = unitOfWork.BlockRelations.Query().Any(b =>
            b.IsActive &&
            b.HospitalityPartnerId == offer.HospitalityPartnerId &&
            b.CharityOrganizationId == organizationId &&
            (b.BlockedByHospitalityPartner || b.BlockedByOrganization));

        if (isBlocked)
        {
            return BadRequest(new { message = "Your organisation is blocked by this hospitality partner and cannot claim their offers." });
        }

        await unitOfWork.OfferMatches.AddAsync(new OfferMatch
        {
            FoodOfferId = offer.Id,
            CharityOrganizationId = organizationId,
            Decision = MatchDecision.Accepted,
            RespondedAtUtc = DateTime.UtcNow,
            Rank = 0,
            Score = 0
        }, cancellationToken);

        offer.Status = FoodOfferStatus.Reserved;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notificationPublisher.NotifyAsync(
            ActorType.HospitalityPartner,
            offer.HospitalityPartnerId,
            "Offer claimed",
            $"An organization claimed your public food offer #{offer.Id}.",
            cancellationToken,
            NotificationType.MatchAccepted,
            ActorType.CharityOrganization,
            organizationId);
        return NoContent();
    }

    [Authorize]
    [HttpGet("{id}/match")]
    public ActionResult GetMatch(int id, CancellationToken cancellationToken)
    {
        var offer = unitOfWork.FoodOffers.Query().FirstOrDefault(o => o.Id == id);
        if (offer is null)
        {
            return NotFound();
        }

        var isAdmin = User.IsAdministrator();
        var isOwnerPartner = User.HospitalityPartnerId() == offer.HospitalityPartnerId;
        var callerOrgId = User.CharityOrganizationId();

        if (!isAdmin && !isOwnerPartner && callerOrgId is null)
        {
            return Forbid();
        }

        if (!isAdmin && !isOwnerPartner && callerOrgId.HasValue)
        {
            var orgMatch = unitOfWork.OfferMatches.Query()
                .Where(m => m.FoodOfferId == id && m.CharityOrganizationId == callerOrgId.Value)
                .OrderBy(m => m.Rank)
                .FirstOrDefault();

            if (orgMatch is null)
            {
                return NotFound(new { message = "Your organization is not in the match list for this offer." });
            }

            return Ok(new[]
            {
                new
                {
                    orgMatch.FoodOfferId,
                    orgMatch.CharityOrganizationId,
                    orgMatch.Score,
                    orgMatch.Rank,
                    orgMatch.Decision,
                    orgMatch.NotifiedAtUtc
                }
            });
        }

        var matches = unitOfWork.OfferMatches.Query()
            .Where(m => m.FoodOfferId == id && m.Rank > 0)
            .OrderBy(m => m.Rank)
            .ToList();

        return Ok(matches);
    }

    [Authorize]
    [HttpPost("{id}/match")]
    public async Task<ActionResult> Match(int id, CancellationToken cancellationToken)
    {
        var offer = await unitOfWork.FoodOffers.GetByIdAsync(id, cancellationToken);
        if (offer is null)
        {
            return NotFound();
        }

        var isAdmin = User.IsAdministrator();
        var isOwnerPartner = User.HospitalityPartnerId() == offer.HospitalityPartnerId;
        var callerOrgId = User.CharityOrganizationId();
        var isCharityOrg = callerOrgId.HasValue;

        if (!isAdmin && !isOwnerPartner && !isCharityOrg)
        {
            return Forbid();
        }

        if (!isAdmin && !isOwnerPartner && isCharityOrg)
        {
            var existingOrgMatch = unitOfWork.OfferMatches.Query()
                .Where(m => m.FoodOfferId == id && m.CharityOrganizationId == callerOrgId!.Value)
                .OrderBy(m => m.Rank)
                .FirstOrDefault();

            if (existingOrgMatch is null)
            {
                return NotFound(new { message = "Your organization is not in the match list for this offer." });
            }

            return Ok(new[]
            {
                new
                {
                    existingOrgMatch.FoodOfferId,
                    existingOrgMatch.CharityOrganizationId,
                    existingOrgMatch.Score,
                    existingOrgMatch.Rank,
                    existingOrgMatch.Decision,
                    existingOrgMatch.NotifiedAtUtc
                }
            });
        }

        if (offer.Status is not (FoodOfferStatus.Active or FoodOfferStatus.PublicFallback))
        {
            return BadRequest(new { message = "Matching is available only for active offers." });
        }

        var alreadyMatched = unitOfWork.OfferMatches.Query().Any(m => m.FoodOfferId == id);
        var matches = await matchingService.GenerateMatchesAsync(id, cancellationToken);
        if (!alreadyMatched)
        {
            var first = matches.FirstOrDefault();
            if (first is not null)
            {
                var firstMatch = unitOfWork.OfferMatches.Query()
                    .Where(m => m.FoodOfferId == id && m.CharityOrganizationId == first.OrganizationId && m.Decision == MatchDecision.Pending)
                    .OrderBy(m => m.Rank)
                    .FirstOrDefault();
                if (firstMatch is not null)
                {
                    firstMatch.NotifiedAtUtc = DateTime.UtcNow;
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }

                await notificationPublisher.NotifyAsync(
                    ActorType.CharityOrganization,
                    first.OrganizationId,
                    "New food offer",
                    $"You are ranked first for offer #{id}. Please accept or decline.",
                    cancellationToken,
                    NotificationType.MatchingOffer,
                    ActorType.HospitalityPartner,
                    offer.HospitalityPartnerId);
            }
        }

        return Ok(matches);
    }

    [Authorize(Roles = "HospitalityPartner,Administrator")]
    [HttpPut("{id}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var offer = unitOfWork.FoodOffers.Query().FirstOrDefault(o => o.Id == id);
        if (offer is null)
        {
            return NotFound();
        }

        var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(offer.HospitalityPartnerId, cancellationToken);
        if (partner is null)
        {
            return NotFound();
        }

        if (!User.IsAdministrator() && User.HospitalityPartnerId() != offer.HospitalityPartnerId)
        {
            return Forbid();
        }

        if (!User.IsAdministrator() && partner.ApprovalStatus != ApprovalStatus.Approved)
        {
            return Forbid();
        }

        if (offer.Matches.Count == 0)
        {
            offer.Matches = unitOfWork.OfferMatches.Query().Where(m => m.FoodOfferId == id).ToList();
        }

        var isRecurrentGraceCancellation = offer.CreatedFromRecurrentDonation && offer.Status == FoodOfferStatus.PendingRestaurantConfirmation;
        offer.Status = FoodOfferStatus.Cancelled;

        if (!isRecurrentGraceCancellation)
        {
            var refreshedPartner = await unitOfWork.HospitalityPartners.GetByIdAsync(offer.HospitalityPartnerId, cancellationToken);
            if (refreshedPartner is not null)
            {
                var previousCancellationCount = refreshedPartner.CancellationCount;
                refreshedPartner.CancellationCount++;
                refreshedPartner.ReputationScore = Math.Max(1, Math.Round(refreshedPartner.ReputationScore - 0.2, 2));
                await unitOfWork.ReputationSnapshots.AddAsync(new ReputationSnapshot
                {
                    ActorType = ActorType.HospitalityPartner,
                    ActorId = refreshedPartner.Id,
                    Score = refreshedPartner.ReputationScore,
                    Source = "Offer cancellation"
                }, cancellationToken);

                if (previousCancellationCount < 3 && refreshedPartner.CancellationCount >= 3)
                {
                    await notificationPublisher.NotifyAsync(
                        ActorType.Administrator,
                        0,
                        "Cancellation warning",
                        $"Hospitality partner #{refreshedPartner.Id} has reached {refreshedPartner.CancellationCount} cancellations.",
                        cancellationToken,
                        NotificationType.AdminMessage);
                }
            }
        }

        var acceptedMatch = offer.Matches.FirstOrDefault(m => m.Decision == MatchDecision.Accepted);
        if (acceptedMatch is not null)
        {
            var activePickup = unitOfWork.PickupDocuments.Query()
                .FirstOrDefault(p =>
                    p.FoodOfferId == offer.Id &&
                    p.CharityOrganizationId == acceptedMatch.CharityOrganizationId &&
                    p.Status != PickupStatus.Cancelled &&
                    p.Status != PickupStatus.PickedUp &&
                    p.Status != PickupStatus.DeliveredToOrganization);

            if (activePickup is not null)
            {
                activePickup.Status = PickupStatus.Cancelled;
                activePickup.CancellationReason = "Offer was cancelled by the hospitality partner.";
                activePickup.CancelledAtUtc = DateTime.UtcNow;

                if (activePickup.DriverId.HasValue)
                {
                    var driver = await unitOfWork.Drivers.GetByIdAsync(activePickup.DriverId.Value, cancellationToken);
                    if (driver is not null)
                    {
                        driver.IsAvailable = true;
                    }
                }

                if (activePickup.VehicleId.HasValue)
                {
                    var vehicle = await unitOfWork.Vehicles.GetByIdAsync(activePickup.VehicleId.Value, cancellationToken);
                    if (vehicle is not null)
                    {
                        vehicle.IsAvailable = true;
                    }
                }
            }

            acceptedMatch.Decision = MatchDecision.Cancelled;
            acceptedMatch.DecisionNote = "Offer was cancelled by the hospitality partner.";
            acceptedMatch.RespondedAtUtc = DateTime.UtcNow;

            var acceptedOrganization = await unitOfWork.CharityOrganizations.GetByIdAsync(acceptedMatch.CharityOrganizationId, cancellationToken);
            if (acceptedOrganization is not null)
            {
                acceptedOrganization.LastReceivedAtUtc = null;
                ApplyCompensationBonus(acceptedOrganization);
            }

            await unitOfWork.PlatformMessages.AddAsync(new PlatformMessage
            {
                SenderType = ActorType.HospitalityPartner,
                SenderId = offer.HospitalityPartnerId,
                RecipientType = ActorType.CharityOrganization,
                RecipientId = acceptedMatch.CharityOrganizationId,
                FoodOfferId = offer.Id,
                MessageType = MessageType.System,
                Body = $"Offer #{id} was cancelled by the hospitality partner."
            }, cancellationToken);
            await notificationPublisher.NotifyAsync(
                ActorType.CharityOrganization,
                acceptedMatch.CharityOrganizationId,
                "Offer cancelled",
                $"Offer #{id} was cancelled.",
                cancellationToken,
                NotificationType.Cancellation,
                ActorType.HospitalityPartner,
                offer.HospitalityPartnerId);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task NotifyFirstMatchAsync(int foodOfferId, CancellationToken cancellationToken)
    {
        var offer = await unitOfWork.FoodOffers.GetByIdAsync(foodOfferId, cancellationToken);
        if (offer is null)
        {
            return;
        }

        var matches = await matchingService.GenerateMatchesAsync(foodOfferId, cancellationToken);
        var first = matches.FirstOrDefault();
        if (first is not null)
        {
            var firstMatch = unitOfWork.OfferMatches.Query()
                .Where(m => m.FoodOfferId == foodOfferId && m.CharityOrganizationId == first.OrganizationId && m.Decision == MatchDecision.Pending)
                .OrderBy(m => m.Rank)
                .FirstOrDefault();
            if (firstMatch is not null)
            {
                firstMatch.NotifiedAtUtc = DateTime.UtcNow;
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            await notificationPublisher.NotifyAsync(
                ActorType.CharityOrganization,
                first.OrganizationId,
                "New food offer",
                $"You are ranked first for offer #{foodOfferId}. Please accept or decline.",
                cancellationToken,
                NotificationType.MatchingOffer,
                ActorType.HospitalityPartner,
                offer.HospitalityPartnerId);
        }
        else
        {
            offer.Status = FoodOfferStatus.PublicFallback;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private static void ApplyCompensationBonus(CharityOrganization organization)
    {
        organization.MatchCompensationBonus = Math.Max(organization.MatchCompensationBonus, 3m);
        organization.MatchCompensationExpiresAtUtc = DateTime.UtcNow.AddDays(3);
    }
}
