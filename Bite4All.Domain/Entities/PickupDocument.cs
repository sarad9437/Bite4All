using Bite4All.Domain.Common;
using Bite4All.Domain.Enums;

namespace Bite4All.Domain.Entities;

public class PickupDocument : Entity
{
    public string DocumentNumber { get; set; } = string.Empty;
    public int FoodOfferId { get; set; }
    public FoodOffer? FoodOffer { get; set; }
    public int HospitalityPartnerId { get; set; }
    public HospitalityPartner? HospitalityPartner { get; set; }
    public int CharityOrganizationId { get; set; }
    public CharityOrganization? CharityOrganization { get; set; }
    public int? DriverId { get; set; }
    public Driver? Driver { get; set; }
    public int? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public PickupStatus Status { get; set; } = PickupStatus.Created;
    public decimal PlannedQuantityKg { get; set; }
    public decimal? ActualQuantityKg { get; set; }
    public string? DriverNote { get; set; }
    public DateTime? PickedUpAtUtc { get; set; }
    /// <summary>
    /// Populated whenever the pickup is cancelled — regardless of which actor
    /// cancelled it (organization, partner offer cancellation, issue resolution).
    /// Provides structured audit trail beyond the SystemMessage that is also sent.
    /// </summary>
    public string? CancellationReason { get; set; }
    /// <summary>
    /// UTC timestamp of when the cancellation occurred. Null if not cancelled.
    /// </summary>
    public DateTime? CancelledAtUtc { get; set; }
}
