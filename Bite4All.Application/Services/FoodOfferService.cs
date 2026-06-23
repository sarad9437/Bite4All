using Bite4All.Application.DTOs.Common;
using Bite4All.Application.DTOs.FoodOffers;
using Bite4All.Application.Extensions;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Domain.Repositories;

namespace Bite4All.Application.Services;

public class FoodOfferService(IUnitOfWork unitOfWork) : IFoodOfferService
{
    public async Task<FoodOfferDto> CreateAsync(CreateFoodOfferRequest request, CancellationToken cancellationToken = default)
    {
        var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(request.HospitalityPartnerId, cancellationToken);
        if (partner is null)
        {
            throw new InvalidOperationException("Hospitality partner does not exist.");
        }

        if (partner.ApprovalStatus != ApprovalStatus.Approved)
        {
            throw new InvalidOperationException("Hospitality partner must be approved before creating offers.");
        }

        var offer = new FoodOffer
        {
            HospitalityPartnerId = request.HospitalityPartnerId,
            HospitalityPartner = partner,
            SpecialCampaignId = request.SpecialCampaignId,
            TotalQuantityKg = request.TotalQuantityKg,
            Category = request.Category,
            PickupWindowStartUtc = request.PickupWindowStartUtc,
            PickupWindowEndUtc = request.PickupWindowEndUtc,
            ExpiresAtUtc = request.ExpiresAtUtc,
            MatchResponseWindowMinutes = request.MatchResponseWindowMinutes > 0 ? request.MatchResponseWindowMinutes : 30,
            Note = request.Note,
            PhotoUrl = request.PhotoUrl,
            Status = FoodOfferStatus.Active
        };

        await unitOfWork.FoodOffers.AddAsync(offer, cancellationToken);
        foreach (var itemRequest in request.Items)
        {
            var item = new FoodOfferItem
            {
                FoodOfferId = offer.Id,
                FoodOffer = offer,
                Name = itemRequest.Name,
                Quantity = itemRequest.Quantity,
                Unit = itemRequest.Unit,
                DietaryTags = itemRequest.DietaryTags
            };
            offer.Items.Add(item);
            await unitOfWork.FoodOfferItems.AddAsync(item, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(offer);
    }

    public async Task<FoodOfferDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var offer = await unitOfWork.FoodOffers.GetByIdAsync(id, cancellationToken);
        if (offer is null)
        {
            return null;
        }

        LoadDetails(offer);
        return ToDto(offer);
    }

    public Task<PagedResult<FoodOfferDto>> GetActiveAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var baseQuery = unitOfWork.FoodOffers.Query()
            .Where(o => o.Status == FoodOfferStatus.Active || o.Status == FoodOfferStatus.PublicFallback)
            .OrderBy(o => o.PickupWindowStartUtc);

        var totalCount = baseQuery.Count();

        var page_items = baseQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        foreach (var offer in page_items)
        {
            LoadDetails(offer);
        }

        return Task.FromResult(new PagedResult<FoodOfferDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = page_items.Select(ToDto).ToList()
        });
    }

    public Task<PagedResult<FoodOfferDto>> SearchAsync(FoodOfferSearchRequest request, CancellationToken cancellationToken = default)
    {
        request.Page = Math.Max(request.Page, 1);
        request.PageSize = Math.Clamp(request.PageSize, 1, 100);

        var baseQuery = unitOfWork.FoodOffers.Query()
            .ApplySearch(request)
            .OrderBy(o => o.PickupWindowStartUtc);

        var totalCount = baseQuery.Count();

        var page_items = baseQuery
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        foreach (var offer in page_items)
        {
            LoadDetails(offer);
        }

        return Task.FromResult(new PagedResult<FoodOfferDto>
        {
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            Items = page_items.Select(ToDto).ToList()
        });
    }

    public async Task<FoodOfferDto?> UpdateAsync(int id, UpdateFoodOfferRequest request, CancellationToken cancellationToken = default)
    {
        var offer = await unitOfWork.FoodOffers.GetByIdAsync(id, cancellationToken);
        if (offer is null)
        {
            return null;
        }

        if (offer.Status is FoodOfferStatus.Completed or FoodOfferStatus.Cancelled)
        {
            throw new InvalidOperationException("Completed or cancelled offers cannot be updated.");
        }

        if (request.TotalQuantityKg.HasValue)
        {
            offer.TotalQuantityKg = request.TotalQuantityKg.Value;
        }

        if (request.Category.HasValue)
        {
            offer.Category = request.Category.Value;
        }

        if (request.PickupWindowStartUtc.HasValue)
        {
            offer.PickupWindowStartUtc = request.PickupWindowStartUtc.Value;
        }

        if (request.PickupWindowEndUtc.HasValue)
        {
            offer.PickupWindowEndUtc = request.PickupWindowEndUtc.Value;
        }

        if (request.ExpiresAtUtc.HasValue)
        {
            offer.ExpiresAtUtc = request.ExpiresAtUtc.Value;
        }

        if (request.MatchResponseWindowMinutes.HasValue && request.MatchResponseWindowMinutes.Value > 0)
        {
            offer.MatchResponseWindowMinutes = request.MatchResponseWindowMinutes.Value;
        }

        if (request.Note is not null)
        {
            offer.Note = request.Note;
        }

        if (request.PhotoUrl is not null)
        {
            offer.PhotoUrl = request.PhotoUrl;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        LoadDetails(offer);
        return ToDto(offer);
    }

    private void LoadDetails(FoodOffer offer)
    {
        offer.HospitalityPartner ??= unitOfWork.HospitalityPartners.Query().FirstOrDefault(p => p.Id == offer.HospitalityPartnerId);
        if (offer.Items.Count == 0)
        {
            offer.Items = unitOfWork.FoodOfferItems.Query().Where(i => i.FoodOfferId == offer.Id).ToList();
        }
    }

    private static FoodOfferDto ToDto(FoodOffer offer)
    {
        return new FoodOfferDto
        {
            Id = offer.Id,
            PartnerName = offer.HospitalityPartner?.Name ?? string.Empty,
            TotalQuantityKg = offer.TotalQuantityKg,
            Category = offer.Category,
            Status = offer.Status,
            PickupWindowStartUtc = offer.PickupWindowStartUtc,
            PickupWindowEndUtc = offer.PickupWindowEndUtc,
            ExpiresAtUtc = offer.ExpiresAtUtc,
            MatchResponseWindowMinutes = offer.MatchResponseWindowMinutes,
            Note = offer.Note,
            PhotoUrl = offer.PhotoUrl,
            Items = offer.Items.Select(i => new FoodOfferItemDto
            {
                Name = i.Name,
                Quantity = i.Quantity,
                Unit = i.Unit,
                DietaryTags = i.DietaryTags
            }).ToList()
        };
    }
}
