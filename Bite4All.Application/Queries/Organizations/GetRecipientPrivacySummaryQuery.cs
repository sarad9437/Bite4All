using Bite4All.Application.DTOs.Organizations;
using Bite4All.Domain.Enums;
using Bite4All.Domain.Repositories;
using MediatR;

namespace Bite4All.Application.Queries.Organizations;

public record GetRecipientPrivacySummaryQuery(int OrganizationId) : IRequest<RecipientPrivacySummaryDto?>;

public class GetRecipientPrivacySummaryQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetRecipientPrivacySummaryQuery, RecipientPrivacySummaryDto?>
{
    public async Task<RecipientPrivacySummaryDto?> Handle(GetRecipientPrivacySummaryQuery request, CancellationToken cancellationToken)
    {
        var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(request.OrganizationId, cancellationToken);
        if (organization is null)
        {
            return null;
        }

        var recipients = unitOfWork.Recipients.Query()
            .Where(r => r.CharityOrganizationId == request.OrganizationId)
            .ToList();

        var flags = new[]
        {
            DietaryTag.Vegetarian,
            DietaryTag.Vegan,
            DietaryTag.GlutenFree,
            DietaryTag.LactoseFree,
            DietaryTag.Halal,
            DietaryTag.Kosher
        };

        return new RecipientPrivacySummaryDto
        {
            OrganizationId = organization.Id,
            OrganizationName = organization.Name,
            TotalRecipients = recipients.Count,
            RecipientsWithoutRestrictions = recipients.Count(r => r.DietaryRestrictions == DietaryTag.None),
            RecipientsWithRestrictions = recipients.Count(r => r.DietaryRestrictions != DietaryTag.None),
            DietaryRestrictions = flags
                .Select(flag => new DietaryRestrictionCountDto
                {
                    Restriction = flag,
                    Count = recipients.Count(r => (r.DietaryRestrictions & flag) == flag)
                })
                .Where(x => x.Count > 0)
                .ToList()
        };
    }
}
