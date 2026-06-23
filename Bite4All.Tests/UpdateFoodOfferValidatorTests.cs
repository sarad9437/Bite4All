using Bite4All.API.Validators;
using Bite4All.Application.DTOs.FoodOffers;

namespace Bite4All.Tests;

public class UpdateFoodOfferValidatorTests
{
    private readonly UpdateFoodOfferRequestValidator _validator = new();

    [Fact]
    public void Empty_request_is_valid_because_all_fields_are_optional()
    {
        var result = _validator.Validate(new UpdateFoodOfferRequest());
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Negative_quantity_is_rejected()
    {
        var result = _validator.Validate(new UpdateFoodOfferRequest { TotalQuantityKg = -5 });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "TotalQuantityKg");
    }

    [Fact]
    public void Zero_quantity_is_rejected()
    {
        var result = _validator.Validate(new UpdateFoodOfferRequest { TotalQuantityKg = 0 });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "TotalQuantityKg");
    }

    [Fact]
    public void Positive_quantity_is_accepted()
    {
        var result = _validator.Validate(new UpdateFoodOfferRequest { TotalQuantityKg = 5 });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Expires_within_two_hours_is_rejected()
    {
        var result = _validator.Validate(new UpdateFoodOfferRequest
        {
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(90)
        });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ExpiresAtUtc");
    }

    [Fact]
    public void Expires_two_hours_or_more_is_accepted()
    {
        var result = _validator.Validate(new UpdateFoodOfferRequest
        {
            ExpiresAtUtc = DateTime.UtcNow.AddHours(3)
        });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Pickup_end_before_start_is_rejected()
    {
        var now = DateTime.UtcNow;
        var result = _validator.Validate(new UpdateFoodOfferRequest
        {
            PickupWindowStartUtc = now.AddHours(2),
            PickupWindowEndUtc = now.AddHours(1)
        });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "PickupWindowEndUtc");
    }

    [Fact]
    public void Response_window_over_1440_minutes_is_rejected()
    {
        var result = _validator.Validate(new UpdateFoodOfferRequest
        {
            MatchResponseWindowMinutes = 1441
        });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "MatchResponseWindowMinutes");
    }

    [Fact]
    public void Response_window_zero_is_rejected()
    {
        var result = _validator.Validate(new UpdateFoodOfferRequest
        {
            MatchResponseWindowMinutes = 0
        });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "MatchResponseWindowMinutes");
    }

    [Fact]
    public void Response_window_1440_is_accepted()
    {
        var result = _validator.Validate(new UpdateFoodOfferRequest
        {
            MatchResponseWindowMinutes = 1440
        });
        Assert.True(result.IsValid);
    }
}
