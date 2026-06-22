using Bite4All.Domain.Enums;

namespace Bite4All.Application.DTOs.Onboarding;

public class CreateRecipientRequest
{
    public int CharityOrganizationId { get; set; }
    public string InternalCode { get; set; } = string.Empty;
    public DietaryTag DietaryRestrictions { get; set; } = DietaryTag.None;
}
