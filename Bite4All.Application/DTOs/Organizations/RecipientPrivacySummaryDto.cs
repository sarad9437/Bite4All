using Bite4All.Domain.Enums;

namespace Bite4All.Application.DTOs.Organizations;

public class RecipientPrivacySummaryDto
{
    public int OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public int TotalRecipients { get; set; }
    public int RecipientsWithoutRestrictions { get; set; }
    public int RecipientsWithRestrictions { get; set; }
    public List<DietaryRestrictionCountDto> DietaryRestrictions { get; set; } = [];
}

public class DietaryRestrictionCountDto
{
    public DietaryTag Restriction { get; set; }
    public int Count { get; set; }
}
