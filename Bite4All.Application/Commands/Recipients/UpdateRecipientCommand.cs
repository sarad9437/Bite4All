using Bite4All.Domain.Enums;
using Bite4All.Domain.Repositories;
using MediatR;

namespace Bite4All.Application.Commands.Recipients;

public record UpdateRecipientCommand(int RecipientId, DietaryTag? DietaryRestrictions) : IRequest<bool>;

public class UpdateRecipientCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateRecipientCommand, bool>
{
    public async Task<bool> Handle(UpdateRecipientCommand request, CancellationToken cancellationToken)
    {
        var recipient = await unitOfWork.Recipients.GetByIdAsync(request.RecipientId, cancellationToken);
        if (recipient is null)
        {
            return false;
        }

        if (request.DietaryRestrictions.HasValue)
        {
            recipient.DietaryRestrictions = request.DietaryRestrictions.Value;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
