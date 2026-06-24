using Bite4All.Domain.Repositories;
using MediatR;

namespace Bite4All.Application.Commands.Drivers;

public record SuspendDriverCommand(int DriverId, string Reason) : IRequest<bool>;

public class SuspendDriverCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<SuspendDriverCommand, bool>
{
    public async Task<bool> Handle(SuspendDriverCommand request, CancellationToken cancellationToken)
    {
        var driver = await unitOfWork.Drivers.GetByIdAsync(request.DriverId, cancellationToken);
        if (driver is null)
        {
            return false;
        }

        if (!driver.IsActive)
        {
            return false; // already suspended
        }

        driver.IsActive = false;
        driver.IsAvailable = false; // cannot be assigned to pickups while suspended
        driver.SuspensionReason = request.Reason;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
