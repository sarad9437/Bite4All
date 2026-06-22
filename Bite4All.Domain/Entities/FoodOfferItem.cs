using Bite4All.Domain.Common;
using Bite4All.Domain.Enums;

namespace Bite4All.Domain.Entities;

public class FoodOfferItem : Entity
{
    public int FoodOfferId { get; set; }
    public FoodOffer? FoodOffer { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "kom";
    public DietaryTag DietaryTags { get; set; } = DietaryTag.None;
}
