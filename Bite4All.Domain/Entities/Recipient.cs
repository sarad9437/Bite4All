using Bite4All.Domain.Common;
using Bite4All.Domain.Enums;

namespace Bite4All.Domain.Entities;

public class Recipient : Entity
{
    public int CharityOrganizationId { get; set; }
    public CharityOrganization? CharityOrganization { get; set; }
    public string InternalCode { get; set; } = string.Empty;
    public DietaryTag DietaryRestrictions { get; set; } = DietaryTag.None;
    public int MealsReceivedCount { get; set; }
}
