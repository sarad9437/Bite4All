using Bite4All.Domain.Repositories;
using MediatR;

namespace Bite4All.Application.Commands.Recipients;

public record DeactivateRecipientCommand(int RecipientId) : IRequest<bool>;

public class DeactivateRecipientCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<DeactivateRecipientCommand, bool>
{
    public async Task<bool> Handle(DeactivateRecipientCommand request, CancellationToken cancellationToken)
    {
        var recipient = await unitOfWork.Recipients.GetByIdAsync(request.RecipientId, cancellationToken);
        if (recipient is null)
        {
            return false;
        }

        if (!recipient.IsActive)
        {
            return false; // already deactivated
        }

        recipient.IsActive = false;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
