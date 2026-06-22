using Bite4All.Domain.Common;
using Bite4All.Domain.Enums;

namespace Bite4All.Domain.Entities;

public class RecipientMealDistribution : Entity
{
    public int PickupDocumentId { get; set; }
    public PickupDocument? PickupDocument { get; set; }
    public int RecipientId { get; set; }
    public Recipient? Recipient { get; set; }
    public int CharityOrganizationId { get; set; }
    public CharityOrganization? CharityOrganization { get; set; }
    public FoodCategory Category { get; set; }
    public int MealsCount { get; set; }
    public DateTime DistributedAtUtc { get; set; } = DateTime.UtcNow;
}
