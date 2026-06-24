using Bite4All.Domain.Repositories;
using MediatR;

namespace Bite4All.Application.Commands.Drivers;

public record UnsuspendDriverCommand(int DriverId) : IRequest<bool>;

public class UnsuspendDriverCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UnsuspendDriverCommand, bool>
{
    public async Task<bool> Handle(UnsuspendDriverCommand request, CancellationToken cancellationToken)
    {
        var driver = await unitOfWork.Drivers.GetByIdAsync(request.DriverId, cancellationToken);
        if (driver is null)
        {
            return false;
        }

        if (driver.IsActive)
        {
            return false; // not suspended
        }

        driver.IsActive = true;
        driver.IsAvailable = true;
        driver.SuspensionReason = null;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
