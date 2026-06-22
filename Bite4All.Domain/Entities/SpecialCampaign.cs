using Bite4All.Domain.Common;
using Bite4All.Domain.Enums;

namespace Bite4All.Domain.Entities;

public class SpecialCampaign : Entity
{
    public int HospitalityPartnerId { get; set; }
    public HospitalityPartner? HospitalityPartner { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public decimal TargetQuantityKg { get; set; }
    public decimal CurrentQuantityKg { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.PendingApproval;
}
