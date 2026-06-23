using Bite4All.Domain.Enums;
using Bite4All.Domain.Repositories;
using MediatR;

namespace Bite4All.Application.Commands.RecurrentDonations;

public record ResumeRecurrentDonationCommand(int RecurrentDonationId) : IRequest<bool>;

public class ResumeRecurrentDonationCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<ResumeRecurrentDonationCommand, bool>
{
    public async Task<bool> Handle(ResumeRecurrentDonationCommand request, CancellationToken cancellationToken)
    {
        var recurrent = await unitOfWork.RecurrentDonations.GetByIdAsync(request.RecurrentDonationId, cancellationToken);
        if (recurrent is null)
        {
            return false;
        }

        if (recurrent.Status != RecurrentDonationStatus.Paused)
        {
            return false;
        }

        recurrent.Status = RecurrentDonationStatus.Active;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
