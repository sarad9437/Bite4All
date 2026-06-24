using Bite4All.Domain.Repositories;
using MediatR;

namespace Bite4All.Application.Commands.Vehicles;

public record DeactivateVehicleCommand(int VehicleId) : IRequest<(bool Success, string? Error)>;

public class DeactivateVehicleCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<DeactivateVehicleCommand, (bool Success, string? Error)>
{
    public async Task<(bool Success, string? Error)> Handle(
        DeactivateVehicleCommand request,
        CancellationToken cancellationToken)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(request.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            return (false, "Vehicle not found.");
        }

        if (!vehicle.IsActive)
        {
            return (false, "Vehicle is already deactivated.");
        }

        // Prevent deactivation if vehicle is currently assigned to an active pickup.
        var hasActivePickup = unitOfWork.PickupDocuments.Query().Any(p =>
            p.VehicleId == request.VehicleId &&
            p.Status != Domain.Enums.PickupStatus.Cancelled &&
            p.Status != Domain.Enums.PickupStatus.DeliveredToOrganization);

        if (hasActivePickup)
        {
            return (false, "Vehicle cannot be deactivated while it is assigned to an active pickup.");
        }

        vehicle.IsActive = false;
        vehicle.IsAvailable = false;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (true, null);
    }
}
