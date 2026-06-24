using Bite4All.Domain.Repositories;
using MediatR;

namespace Bite4All.Application.Commands.Drivers;

public record UpdateDriverCommand(int DriverId, string? FullName, string? Phone) : IRequest<bool>;

public class UpdateDriverCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateDriverCommand, bool>
{
    public async Task<bool> Handle(UpdateDriverCommand request, CancellationToken cancellationToken)
    {
        var driver = await unitOfWork.Drivers.GetByIdAsync(request.DriverId, cancellationToken);
        if (driver is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            driver.FullName = request.FullName;
        }

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            driver.Phone = request.Phone;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
