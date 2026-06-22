using Bite4All.Application.DTOs.Pickups;
using FluentValidation;

namespace Bite4All.API.Validators;

public class AssignPickupRequestValidator : AbstractValidator<AssignPickupRequest>
{
    public AssignPickupRequestValidator()
    {
        RuleFor(r => r.DriverId)
            .GreaterThan(0)
            .WithMessage("Driver is required.");

        RuleFor(r => r.VehicleId)
            .GreaterThan(0)
            .WithMessage("Vehicle is required.");
    }
}
