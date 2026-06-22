using Bite4All.Domain.Enums;
using Bite4All.Domain.Repositories;
using MediatR;

namespace Bite4All.Application.Commands.RecurrentDonations;

public record CancelRecurrentDonationCommand(int RecurrentDonationId) : IRequest<bool>;

public class CancelRecurrentDonationCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<CancelRecurrentDonationCommand, bool>
{
    public async Task<bool> Handle(CancelRecurrentDonationCommand request, CancellationToken cancellationToken)
    {
        var recurrent = await unitOfWork.RecurrentDonations.GetByIdAsync(request.RecurrentDonationId, cancellationToken);
        if (recurrent is null)
        {
            return false;
        }

        recurrent.Status = RecurrentDonationStatus.Cancelled;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
