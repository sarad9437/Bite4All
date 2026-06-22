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
    IMatchingService matchingService,
    Bite4All.API.Hubs.INotificationPublisher notificationPublisher,
    IUnitOfWork unitOfWork,
    IWebHostEnvironment environment,
    ISender sender) : ControllerBase
{
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

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FoodOfferDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var offer = await unitOfWork.FoodOffers.GetByIdAsync(id, cancellationToken);
        if (offer is null)
        {
            return NotFound();
        }

        var isPublic = offer.Status is FoodOfferStatus.Active or FoodOfferStatus.PublicFallback;
        var isOwner = User.IsAdministrator() || User.HospitalityPartnerId() == offer.HospitalityPartnerId;
        var isMatchedOrganization = User.CharityOrganizationId() is int organizationId &&
                                    unitOfWork.OfferMatches.Query().Any(m => m.FoodOfferId == offer.Id && m.CharityOrganizationId == organizationId);

        if (!isPublic)
        {
            if (!(User.Identity?.IsAuthenticated ?? false))
            {
                return Forbid();
            }

            if (!isOwner && !isMatchedOrganization)
            {
                return Forbid();
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

        try
        {
            return Ok(await foodOfferService.UpdateAsync(id, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

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

        var root = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        var uploadDirectory = Path.Combine(root, "uploads", "food-offers");
        Directory.CreateDirectory(uploadDirectory);

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
        return CreatedAtAction(nameof(GetActive), new { id = recurrent.Id }, recurrent);
    }

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

        var today = DateTime.UtcNow.Date;
        var request = new CreateFoodOfferRequest
        {
            HospitalityPartnerId = recurrent.HospitalityPartnerId,
            TotalQuantityKg = recurrent.ExpectedQuantityKg,
            Category = recurrent.Category,
            PickupWindowStartUtc = today.Add(recurrent.LocalPickupStart.ToTimeSpan()),
            PickupWindowEndUtc = today.Add(recurrent.LocalPickupEnd.ToTimeSpan()),
            ExpiresAtUtc = today.Add(recurrent.LocalPickupEnd.ToTimeSpan()).AddHours(Math.Max(recurrent.ShelfLifeHours, 2)),
            Note = recurrent.NoteTemplate,
            Items =
            [
                new CreateFoodOfferItemRequest
                {
                    Name = "Recurrent donation",
                    Quantity = recurrent.ExpectedQuantityKg,
                    Unit = "kg"
                }
            ]
        };

        var result = await foodOfferService.CreateAsync(request, cancellationToken);
        var offer = await unitOfWork.FoodOffers.GetByIdAsync(result.Id, cancellationToken);
        if (offer is not null)
        {
            offer.CreatedFromRecurrentDonation = true;
            offer.Status = FoodOfferStatus.PendingRestaurantConfirmation;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
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
        await NotifyFirstMatchAsync(offer.Id, cancellationToken);
        return NoContent();
    }

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

        if (!User.IsAdministrator())
        {
            var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(User.CharityOrganizationId() ?? 0, cancellationToken);
            if (organization is null)
            {
                return Forbid();
            }

            if (organization.ApprovalStatus != ApprovalStatus.Approved)
            {
                return Forbid();
            }
        }

        var organizationId = User.IsAdministrator()
            ? unitOfWork.CharityOrganizations.Query().Where(o => o.ApprovalStatus == ApprovalStatus.Approved).Select(o => o.Id).FirstOrDefault()
            : User.CharityOrganizationId();

        if (organizationId is null or 0)
        {
            return Forbid();
        }

        await unitOfWork.OfferMatches.AddAsync(new OfferMatch
        {
            FoodOfferId = offer.Id,
            CharityOrganizationId = organizationId.Value,
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
            organizationId.Value);
        return NoContent();
    }

    [Authorize(Roles = "Administrator")]
    [HttpPost("{id}/match")]
    public async Task<ActionResult> Match(int id, CancellationToken cancellationToken)
    {
        var offer = await unitOfWork.FoodOffers.GetByIdAsync(id, cancellationToken);
        if (offer is null)
        {
            return NotFound();
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
