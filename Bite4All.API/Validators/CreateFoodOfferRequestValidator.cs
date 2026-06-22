using Bite4All.Application.DTOs.FoodOffers;
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

        RuleFor(r => r.PickupWindowEndUtc)
            .GreaterThan(r => r.PickupWindowStartUtc)
            .WithMessage("Pickup window end must be after start.");

        RuleFor(r => r.ExpiresAtUtc)
            .GreaterThanOrEqualTo(_ => DateTime.UtcNow.AddHours(2))
            .WithMessage("Food must be valid for at least two hours from offer creation.");

        RuleFor(r => r.MatchResponseWindowMinutes)
            .GreaterThan(0)
            .WithMessage("Match response window must be greater than zero.");

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
