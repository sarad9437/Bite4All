namespace Bite4All.Application.DTOs.FoodOffers;

public class CreateCampaignRequest
{
    public int HospitalityPartnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public decimal TargetQuantityKg { get; set; }
}
