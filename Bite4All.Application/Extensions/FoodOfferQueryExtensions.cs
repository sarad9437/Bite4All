using Bite4All.Application.DTOs.FoodOffers;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;

namespace Bite4All.Application.Extensions;

public static class FoodOfferQueryExtensions
{
    public static IQueryable<FoodOffer> ApplySearch(this IQueryable<FoodOffer> query, FoodOfferSearchRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.PartnerName))
        {
            query = query.Where(o => o.HospitalityPartner != null && o.HospitalityPartner.Name.Contains(request.PartnerName));
        }

        if (request.CityId.HasValue)
        {
            query = query.Where(o => o.HospitalityPartner != null && o.HospitalityPartner.CityId == request.CityId.Value);
        }

        if (request.Category.HasValue)
        {
            query = query.Where(o => o.Category == request.Category.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(o => o.Status == request.Status.Value);
        }
        else
        {
            query = query.Where(o => o.Status != FoodOfferStatus.Cancelled && o.Status != FoodOfferStatus.Expired);
        }

        if (request.MinQuantityKg.HasValue)
        {
            query = query.Where(o => o.TotalQuantityKg >= request.MinQuantityKg.Value);
        }

        if (request.MaxQuantityKg.HasValue)
        {
            query = query.Where(o => o.TotalQuantityKg <= request.MaxQuantityKg.Value);
        }

        if (request.PickupFromUtc.HasValue)
        {
            query = query.Where(o => o.PickupWindowStartUtc >= request.PickupFromUtc.Value);
        }

        if (request.PickupToUtc.HasValue)
        {
            query = query.Where(o => o.PickupWindowEndUtc <= request.PickupToUtc.Value);
        }

        return query;
    }
}
