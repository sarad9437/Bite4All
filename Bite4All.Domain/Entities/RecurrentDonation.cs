using Bite4All.Domain.Common;
using Bite4All.Domain.Enums;

namespace Bite4All.Domain.Entities;

public class RecurrentDonation : Entity
{
    public int HospitalityPartnerId { get; set; }
    public HospitalityPartner? HospitalityPartner { get; set; }
    public decimal ExpectedQuantityKg { get; set; }
    public FoodCategory Category { get; set; }
    public TimeOnly LocalCreationTime { get; set; }
    public TimeOnly LocalPickupStart { get; set; }
    public TimeOnly LocalPickupEnd { get; set; }
    public int ShelfLifeHours { get; set; }
    public string? NoteTemplate { get; set; }
    public RecurrentDonationStatus Status { get; set; } = RecurrentDonationStatus.Active;
}
