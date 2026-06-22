using Bite4All.API.Validators;
using Bite4All.Application.DTOs.FoodOffers;
using Bite4All.Domain.Enums;

namespace Bite4All.Tests;

public class CreateFoodOfferRequestValidatorTests
{
    [Fact]
    public void Validator_rejects_food_that_expires_too_soon()
    {
        var validator = new CreateFoodOfferRequestValidator();
        var request = new CreateFoodOfferRequest
        {
            HospitalityPartnerId = 1,
            TotalQuantityKg = 5,
            Category = FoodCategory.Bakery,
            PickupWindowStartUtc = DateTime.UtcNow.AddMinutes(30),
            PickupWindowEndUtc = DateTime.UtcNow.AddHours(1),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(90),
            Items = [new CreateFoodOfferItemRequest { Name = "Hleb", Quantity = 5, Unit = "kom" }]
        };

        var validationResult = validator.Validate(request);

        Assert.False(validationResult.IsValid);
        Assert.Contains(validationResult.Errors, error => error.ErrorMessage.Contains("Food must be valid", StringComparison.OrdinalIgnoreCase));
    }
}
