using Bite4All.Domain.Enums;
using Bite4All.Domain.Repositories;
using MediatR;

namespace Bite4All.Application.Commands.RecurrentDonations;

public record PauseRecurrentDonationCommand(int RecurrentDonationId) : IRequest<bool>;

public class PauseRecurrentDonationCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<PauseRecurrentDonationCommand, bool>
{
    public async Task<bool> Handle(PauseRecurrentDonationCommand request, CancellationToken cancellationToken)
    {
        var recurrent = await unitOfWork.RecurrentDonations.GetByIdAsync(request.RecurrentDonationId, cancellationToken);
        if (recurrent is null)
        {
            return false;
        }

        if (recurrent.Status == RecurrentDonationStatus.Cancelled)
        {
            return false;
        }

        recurrent.Status = RecurrentDonationStatus.Paused;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
