using Bite4All.Application.DTOs.Common;
using Bite4All.Application.DTOs.FoodOffers;

namespace Bite4All.Application.Services;

public interface IFoodOfferService
{
    Task<FoodOfferDto> CreateAsync(CreateFoodOfferRequest request, CancellationToken cancellationToken = default);
    Task<FoodOfferDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PagedResult<FoodOfferDto>> GetActiveAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<PagedResult<FoodOfferDto>> SearchAsync(FoodOfferSearchRequest request, CancellationToken cancellationToken = default);
    Task<FoodOfferDto?> UpdateAsync(int id, UpdateFoodOfferRequest request, CancellationToken cancellationToken = default);
}
