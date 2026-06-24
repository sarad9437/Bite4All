using Bite4All.Application.DTOs.FoodOffers;
using FluentValidation;

namespace Bite4All.API.Validators;

/// <summary>
/// Validates UpdateFoodOfferRequest.
/// All fields are optional (partial update) but when provided must be valid.
/// </summary>
public class UpdateFoodOfferRequestValidator : AbstractValidator<UpdateFoodOfferRequest>
{
    public UpdateFoodOfferRequestValidator()
    {
        RuleFor(r => r.TotalQuantityKg)
            .GreaterThan(0)
            .WithMessage("Total quantity must be greater than zero.")
            .When(r => r.TotalQuantityKg.HasValue);

        // Fix: pickup window start must be in the future when provided.
        RuleFor(r => r.PickupWindowStartUtc)
            .GreaterThan(_ => DateTime.UtcNow)
            .WithMessage("Pickup window start must be in the future.")
            .When(r => r.PickupWindowStartUtc.HasValue);

        RuleFor(r => r.PickupWindowEndUtc)
            .GreaterThan(r => r.PickupWindowStartUtc!.Value)
            .WithMessage("Pickup window end must be after start.")
            .When(r => r.PickupWindowStartUtc.HasValue && r.PickupWindowEndUtc.HasValue);

        RuleFor(r => r.ExpiresAtUtc)
            .GreaterThanOrEqualTo(_ => DateTime.UtcNow.AddHours(2))
            .WithMessage("Food must be valid for at least two hours from now.")
            .When(r => r.ExpiresAtUtc.HasValue);

        // Fix: expiry must not be before the pickup window closes.
        RuleFor(r => r.ExpiresAtUtc)
            .GreaterThanOrEqualTo(r => r.PickupWindowEndUtc!.Value)
            .WithMessage("Food expiry must be at or after the end of the pickup window.")
            .When(r => r.ExpiresAtUtc.HasValue && r.PickupWindowEndUtc.HasValue);

        RuleFor(r => r.MatchResponseWindowMinutes)
            .GreaterThan(0)
            .WithMessage("Match response window must be greater than zero.")
            .LessThanOrEqualTo(1440)
            .WithMessage("Match response window cannot exceed 1440 minutes (24 hours).")
            .When(r => r.MatchResponseWindowMinutes.HasValue);
    }
}
