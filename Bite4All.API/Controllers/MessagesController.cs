using Bite4All.API.Authorization;
using Bite4All.API.Hubs;
using Bite4All.Application.DTOs.Common;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bite4All.API.Controllers;

[ApiController]
[Authorize]
[Route("messages")]
public class MessagesController(IUnitOfWork unitOfWork, INotificationPublisher notificationPublisher) : ControllerBase
{
    private static readonly MatchDecision[] SkippedMatchDecisions =
    [
        MatchDecision.SkippedByBlock,
        MatchDecision.SkippedByCapacity,
        MatchDecision.SkippedByDiet
    ];

    [HttpGet("pickup/{pickupDocumentId}")]
    public async Task<ActionResult<PagedResult<PlatformMessage>>> GetForPickup(
        int pickupDocumentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] ActorType? senderType = null,
        [FromQuery] MessageType? messageType = null,
        CancellationToken cancellationToken = default)
    {
        var pickup = await unitOfWork.PickupDocuments.GetByIdAsync(pickupDocumentId, cancellationToken);
        if (pickup is null)
        {
            return NotFound();
        }

        if (!CanAccessPickup(pickup))
        {
            return Forbid();
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = unitOfWork.PlatformMessages.Query()
            .Where(m => m.PickupDocumentId == pickupDocumentId);

        if (senderType.HasValue)
        {
            query = query.Where(m => m.SenderType == senderType.Value);
        }

        if (messageType.HasValue)
        {
            query = query.Where(m => m.MessageType == messageType.Value);
        }

        var totalCount = query.Count();
        var items = query
            .OrderBy(m => m.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new PagedResult<PlatformMessage>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    [HttpGet("offer/{foodOfferId}")]
    public async Task<ActionResult<PagedResult<PlatformMessage>>> GetForOffer(
        int foodOfferId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] ActorType? senderType = null,
        [FromQuery] MessageType? messageType = null,
        CancellationToken cancellationToken = default)
    {
        var offer = await unitOfWork.FoodOffers.GetByIdAsync(foodOfferId, cancellationToken);
        if (offer is null)
        {
            return NotFound();
        }

        if (!CanAccessOffer(offer))
        {
            return Forbid();
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = unitOfWork.PlatformMessages.Query()
            .Where(m => m.FoodOfferId == foodOfferId);

        if (senderType.HasValue)
        {
            query = query.Where(m => m.SenderType == senderType.Value);
        }

        if (messageType.HasValue)
        {
            query = query.Where(m => m.MessageType == messageType.Value);
        }

        var totalCount = query.Count();
        var items = query
            .OrderBy(m => m.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new PagedResult<PlatformMessage>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    [HttpPost]
    public async Task<ActionResult<PlatformMessage>> Send(CreateMessageRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return BadRequest(new { message = "Message body is required." });
        }

        if (!User.IsAdministrator())
        {
            if (!request.SenderId.HasValue || request.SenderId <= 0)
            {
                return BadRequest(new { message = "SenderId is required." });
            }

            var senderMatchesUser =
                request.SenderType == ActorType.HospitalityPartner && User.HospitalityPartnerId() == request.SenderId ||
                request.SenderType == ActorType.CharityOrganization && User.CharityOrganizationId() == request.SenderId ||
                request.SenderType == ActorType.Driver && User.DriverId() == request.SenderId;

            if (!senderMatchesUser)
            {
                return Forbid();
            }
        }

        if (request.SenderType != ActorType.Administrator && request.PickupDocumentId is null && request.FoodOfferId is null)
        {
            return BadRequest(new { message = "Messages must be tied to an offer or pickup unless sent by administrator." });
        }

        if (request.SenderType == ActorType.HospitalityPartner)
        {
            if (request.FoodOfferId is null || request.PickupDocumentId is not null)
            {
                return BadRequest(new { message = "Hospitality partners can message only in the context of a food offer." });
            }

            var offer = await unitOfWork.FoodOffers.GetByIdAsync(request.FoodOfferId.Value, cancellationToken);
            if (offer is null)
            {
                return NotFound();
            }

            var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(offer.HospitalityPartnerId, cancellationToken);
            if (partner is null)
            {
                return NotFound();
            }

            if (offer.HospitalityPartnerId != request.SenderId)
            {
                return Forbid();
            }

            if (partner.ApprovalStatus != ApprovalStatus.Approved)
            {
                return Forbid();
            }

            if (request.RecipientType != ActorType.CharityOrganization)
            {
                return BadRequest(new { message = "Hospitality partner messages must target a charity organization." });
            }

            if (!unitOfWork.OfferMatches.Query().Any(m =>
                    m.FoodOfferId == offer.Id &&
                    m.CharityOrganizationId == request.RecipientId &&
                    !SkippedMatchDecisions.Contains(m.Decision)))
            {
                return BadRequest(new { message = "Offer messages must target an organization involved in the offer." });
            }
        }
        else if (request.SenderType == ActorType.CharityOrganization)
        {
            if (request.PickupDocumentId is null && request.FoodOfferId is null)
            {
                return BadRequest(new { message = "Charity organizations must message in the context of an offer or a pickup." });
            }

            if (request.FoodOfferId.HasValue)
            {
                if (request.PickupDocumentId is not null)
                {
                    return BadRequest(new { message = "Charity organizations must target either an offer or a pickup, not both." });
                }

                var offer = await unitOfWork.FoodOffers.GetByIdAsync(request.FoodOfferId.Value, cancellationToken);
                if (offer is null)
                {
                    return NotFound();
                }

                var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(request.SenderId!.Value, cancellationToken);
                if (organization is null || organization.ApprovalStatus != ApprovalStatus.Approved)
                {
                    return Forbid();
                }

                var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(offer.HospitalityPartnerId, cancellationToken);
                if (partner is null || partner.ApprovalStatus != ApprovalStatus.Approved)
                {
                    return Forbid();
                }

                if (request.RecipientType != ActorType.HospitalityPartner)
                {
                    return BadRequest(new { message = "Offer messages from organizations must target the hospitality partner attached to that offer." });
                }

                if (request.RecipientId != offer.HospitalityPartnerId)
                {
                    return BadRequest(new { message = "Offer messages must target the hospitality partner attached to that offer." });
                }

                if (!unitOfWork.OfferMatches.Query().Any(m =>
                        m.FoodOfferId == offer.Id &&
                        m.CharityOrganizationId == request.SenderId &&
                        !SkippedMatchDecisions.Contains(m.Decision)))
                {
                    return BadRequest(new { message = "Offer messages must target an organization involved in the offer." });
                }
            }
            else
            {
                if (request.PickupDocumentId is null)
                {
                    return BadRequest(new { message = "Pickup messages require a pickup document." });
                }

                var pickup = await unitOfWork.PickupDocuments.GetByIdAsync(request.PickupDocumentId.Value, cancellationToken);
                if (pickup is null)
                {
                    return NotFound();
                }

                if (pickup.CharityOrganizationId != request.SenderId)
                {
                    return Forbid();
                }

                var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(pickup.CharityOrganizationId, cancellationToken);
                if (organization is null || organization.ApprovalStatus != ApprovalStatus.Approved)
                {
                    return Forbid();
                }

                if (request.RecipientType is not (ActorType.HospitalityPartner or ActorType.Driver))
                {
                    return BadRequest(new { message = "Pickup messages must target the partner or the driver attached to that pickup." });
                }

                if (request.RecipientType == ActorType.HospitalityPartner && request.RecipientId != pickup.HospitalityPartnerId)
                {
                    return BadRequest(new { message = "Pickup messages must target the partner attached to that pickup." });
                }

                if (request.RecipientType == ActorType.Driver && pickup.DriverId.HasValue && request.RecipientId != pickup.DriverId)
                {
                    return BadRequest(new { message = "Pickup messages must target the driver attached to that pickup." });
                }

                if (request.RecipientType == ActorType.Driver && !pickup.DriverId.HasValue)
                {
                    return BadRequest(new { message = "Pickup does not have an assigned driver." });
                }
            }
        }
        else if (request.SenderType == ActorType.Driver)
        {
            if (request.PickupDocumentId is null || request.FoodOfferId is not null)
            {
                return BadRequest(new { message = "Drivers can message only in the context of a pickup." });
            }

            var pickup = await unitOfWork.PickupDocuments.GetByIdAsync(request.PickupDocumentId.Value, cancellationToken);
            if (pickup is null)
            {
                return NotFound();
            }

            if (pickup.DriverId != request.SenderId)
            {
                return Forbid();
            }

            var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(pickup.CharityOrganizationId, cancellationToken);
            if (organization is null || organization.ApprovalStatus != ApprovalStatus.Approved)
            {
                return Forbid();
            }

            if (request.RecipientType == ActorType.HospitalityPartner && request.RecipientId != pickup.HospitalityPartnerId)
            {
                return BadRequest(new { message = "Pickup messages must target the partner attached to that pickup." });
            }

            if (request.RecipientType == ActorType.CharityOrganization && request.RecipientId != pickup.CharityOrganizationId)
            {
                return BadRequest(new { message = "Pickup messages must target the organization attached to that pickup." });
            }

            if (request.RecipientType == ActorType.Driver && request.RecipientId != pickup.DriverId)
            {
                return BadRequest(new { message = "Pickup messages must target the driver attached to that pickup." });
            }
        }

        var message = new PlatformMessage
        {
            SenderType = request.SenderType,
            SenderId = request.SenderId,
            RecipientType = request.RecipientType,
            RecipientId = request.RecipientId,
            PickupDocumentId = request.PickupDocumentId,
            FoodOfferId = request.FoodOfferId,
            MessageType = request.SenderType == ActorType.Administrator ? MessageType.Admin : MessageType.User,
            Body = request.Body
        };

        await unitOfWork.PlatformMessages.AddAsync(message, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notificationPublisher.NotifyAsync(
            request.RecipientType,
            request.RecipientId,
            "New message",
            request.Body,
            cancellationToken,
            NotificationType.AdminMessage,
            request.SenderType,
            request.SenderId);
        return Ok(message);
    }

    private bool CanAccessPickup(PickupDocument pickup)
    {
        return User.IsAdministrator()
            || User.HospitalityPartnerId() == pickup.HospitalityPartnerId
            || User.CharityOrganizationId() == pickup.CharityOrganizationId
            || User.DriverId() == pickup.DriverId;
    }

    private bool CanAccessOffer(FoodOffer offer)
    {
        if (User.IsAdministrator() || User.HospitalityPartnerId() == offer.HospitalityPartnerId)
        {
            return true;
        }

        var orgId = User.CharityOrganizationId();
        if (!orgId.HasValue)
        {
            return false;
        }

        return unitOfWork.OfferMatches.Query().Any(m =>
            m.FoodOfferId == offer.Id &&
            m.CharityOrganizationId == orgId.Value &&
            !SkippedMatchDecisions.Contains(m.Decision));
    }
}
