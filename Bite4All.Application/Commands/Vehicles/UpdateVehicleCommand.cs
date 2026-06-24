using Bite4All.Domain.Repositories;
using MediatR;

namespace Bite4All.Application.Commands.Vehicles;

public record UpdateVehicleCommand(
    int VehicleId,
    string? RegistrationNumber,
    decimal? CapacityKg,
    bool? HasCooling) : IRequest<bool>;

public class UpdateVehicleCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateVehicleCommand, bool>
{
    public async Task<bool> Handle(UpdateVehicleCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(request.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.RegistrationNumber))
        {
            vehicle.RegistrationNumber = request.RegistrationNumber;
        }

        if (request.CapacityKg.HasValue && request.CapacityKg.Value > 0)
        {
            vehicle.CapacityKg = request.CapacityKg.Value;
        }

        if (request.HasCooling.HasValue)
        {
            vehicle.HasCooling = request.HasCooling.Value;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
