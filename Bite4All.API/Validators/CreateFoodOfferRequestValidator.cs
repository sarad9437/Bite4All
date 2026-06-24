using Bite4All.Application.DTOs.FoodOffers;
using Bite4All.Domain.Enums;
using FluentValidation;

namespace Bite4All.API.Validators;

public class CreateFoodOfferRequestValidator : AbstractValidator<CreateFoodOfferRequest>
{
    public CreateFoodOfferRequestValidator()
    {
        RuleFor(r => r.HospitalityPartnerId)
            .GreaterThan(0)
            .WithMessage("Hospitality partner is required.");

        RuleFor(r => r.TotalQuantityKg)
            .GreaterThan(0)
            .WithMessage("Total quantity must be greater than zero.");

        // FoodCategory enum starts at 1 — reject default 0 value.
        RuleFor(r => r.Category)
            .Must(c => Enum.IsDefined(typeof(FoodCategory), c) && c != 0)
            .WithMessage("A valid food category is required.");

        // Fix: pickup window start must be in the future — no point accepting
        // a past pickup window since no organization would be able to collect the food.
        RuleFor(r => r.PickupWindowStartUtc)
            .GreaterThan(_ => DateTime.UtcNow)
            .WithMessage("Pickup window start must be in the future.");

        RuleFor(r => r.PickupWindowEndUtc)
            .GreaterThan(r => r.PickupWindowStartUtc)
            .WithMessage("Pickup window end must be after start.");

        RuleFor(r => r.ExpiresAtUtc)
            .GreaterThanOrEqualTo(_ => DateTime.UtcNow.AddHours(2))
            .WithMessage("Food must be valid for at least two hours from offer creation.");

        // Fix: food cannot expire before the pickup window closes — that would
        // mean the food is already expired by the time the driver arrives.
        RuleFor(r => r.ExpiresAtUtc)
            .GreaterThanOrEqualTo(r => r.PickupWindowEndUtc)
            .WithMessage("Food expiry must be at or after the end of the pickup window.");

        // No more than 24 hours (1440 minutes) response window.
        RuleFor(r => r.MatchResponseWindowMinutes)
            .GreaterThan(0)
            .WithMessage("Match response window must be greater than zero.")
            .LessThanOrEqualTo(1440)
            .WithMessage("Match response window cannot exceed 1440 minutes (24 hours).");

        RuleFor(r => r.Items)
            .NotEmpty()
            .WithMessage("At least one offer item is required.");

        RuleForEach(r => r.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Name)
                .NotEmpty()
                .WithMessage("Item name is required.");

            item.RuleFor(i => i.Quantity)
                .GreaterThan(0)
                .WithMessage(i => $"Item '{i.Name}' quantity must be greater than zero.");

            item.RuleFor(i => i.Unit)
                .NotEmpty()
                .WithMessage(i => $"Item '{i.Name}' unit is required.");
        });
    }
}
