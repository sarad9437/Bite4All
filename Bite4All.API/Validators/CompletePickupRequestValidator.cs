using Bite4All.Application.DTOs.Pickups;
using FluentValidation;

namespace Bite4All.API.Validators;

/// <summary>
/// Fix: ActualQuantityKg had no validation at the controller level — PickupsController.Complete
/// never injected/validated this validator, so negative or zero quantities could be saved
/// (e.g. corrupting impact reports and CO2 calculations). Now enforced with an upper sanity
/// bound too, since an absurd value (e.g. 999999kg) would also corrupt aggregate reports.
/// </summary>
public class CompletePickupRequestValidator : AbstractValidator<CompletePickupRequest>
{
    private const decimal MaxReasonableQuantityKg = 5000m;

    public CompletePickupRequestValidator()
    {
        RuleFor(r => r.ActualQuantityKg)
            .GreaterThan(0)
            .WithMessage("Actual quantity must be greater than zero.")
            .LessThanOrEqualTo(MaxReasonableQuantityKg)
            .WithMessage($"Actual quantity exceeds the maximum reasonable amount of {MaxReasonableQuantityKg}kg for a single pickup.");
    }
}
