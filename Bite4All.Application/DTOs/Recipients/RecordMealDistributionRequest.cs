using Bite4All.Domain.Enums;

namespace Bite4All.Application.DTOs.Recipients;

public class RecordMealDistributionRequest
{
    public int PickupDocumentId { get; set; }
    public int RecipientId { get; set; }
    public int MealsCount { get; set; }
    public FoodCategory Category { get; set; }
}
