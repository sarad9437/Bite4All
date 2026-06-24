using Bite4All.Domain.Repositories;
using MediatR;

namespace Bite4All.Application.Commands.Vehicles;

public record UpdateVehicleCommand(
    int VehicleId,
    string? RegistrationNumber,
    decimal? CapacityKg,
    bool? HasCooling) : IRequest<(bool Success, string? Error)>;

public class UpdateVehicleCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateVehicleCommand, (bool Success, string? Error)>
{
    public async Task<(bool Success, string? Error)> Handle(UpdateVehicleCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(request.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            return (false, "Vehicle not found.");
        }

        if (!vehicle.IsActive)
        {
            return (false, "Cannot update a deactivated vehicle.");
        }

        if (!string.IsNullOrWhiteSpace(request.RegistrationNumber))
        {
            vehicle.RegistrationNumber = request.RegistrationNumber;
        }

        if (request.CapacityKg.HasValue)
        {
            if (request.CapacityKg.Value <= 0)
            {
                return (false, "Capacity must be greater than zero.");
            }
            vehicle.CapacityKg = request.CapacityKg.Value;
        }

        if (request.HasCooling.HasValue)
        {
            vehicle.HasCooling = request.HasCooling.Value;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (true, null);
    }
}
