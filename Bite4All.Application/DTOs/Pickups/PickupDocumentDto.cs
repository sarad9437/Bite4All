using Bite4All.Domain.Enums;

namespace Bite4All.Application.DTOs.Pickups;

public class PickupDocumentDto
{
    public int Id { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string? DriverName { get; set; }
    public string? VehicleRegistrationNumber { get; set; }
    public PickupStatus Status { get; set; }
    public decimal PlannedQuantityKg { get; set; }
    public decimal? ActualQuantityKg { get; set; }
    public DateTime? PickedUpAtUtc { get; set; }
    public decimal? DriverLatitude { get; set; }
    public decimal? DriverLongitude { get; set; }
    public DateTime? DriverLocationUpdatedAtUtc { get; set; }
}
