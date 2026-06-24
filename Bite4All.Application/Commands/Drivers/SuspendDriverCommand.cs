using Bite4All.Domain.Enums;
using Bite4All.Domain.Repositories;
using MediatR;

namespace Bite4All.Application.Commands.Drivers;

public record SuspendDriverCommand(int DriverId, string Reason) : IRequest<(bool Success, string? Error)>;

public class SuspendDriverCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<SuspendDriverCommand, (bool Success, string? Error)>
{
    public async Task<(bool Success, string? Error)> Handle(SuspendDriverCommand request, CancellationToken cancellationToken)
    {
        var driver = await unitOfWork.Drivers.GetByIdAsync(request.DriverId, cancellationToken);
        if (driver is null)
        {
            return (false, "Driver not found.");
        }

        if (!driver.IsActive)
        {
            return (false, "Driver is already suspended.");
        }

        // Fix 4: warn if driver has active pickups — suspension while on a pickup could leave
        // the pickup stuck with no driver. We still allow it (admin/org may need to force-suspend)
        // but return the info so the caller can surface it to the user.
        // The controller will pass this error message back as a 400 so the UI can warn.
        var hasActivePickup = unitOfWork.PickupDocuments.Query().Any(p =>
            p.DriverId == request.DriverId &&
            p.Status != PickupStatus.Cancelled &&
            p.Status != PickupStatus.DeliveredToOrganization &&
            p.Status != PickupStatus.PickedUp);

        if (hasActivePickup)
        {
            return (false, "Driver has an active pickup in progress. Reassign or cancel it before suspending.");
        }

        driver.IsActive = false;
        driver.IsAvailable = false;
        driver.SuspensionReason = request.Reason;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (true, null);
    }
}
