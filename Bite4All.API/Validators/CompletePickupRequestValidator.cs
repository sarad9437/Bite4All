using Bite4All.Application.DTOs.Pickups;
using FluentValidation;

namespace Bite4All.API.Validators;

public class CompletePickupRequestValidator : AbstractValidator<CompletePickupRequest>
{
    public CompletePickupRequestValidator()
    {
        RuleFor(r => r.ActualQuantityKg)
            .GreaterThan(0)
            .WithMessage("Actual quantity must be greater than zero.");
    }
}
