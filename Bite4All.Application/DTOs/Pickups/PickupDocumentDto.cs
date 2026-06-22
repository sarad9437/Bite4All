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
    /// <summary>
    /// Detaljan spisak stavki hrane iz ponude vezane za ovo preuzimanje.
    /// Spec: "Dokument sadrži... detaljan spisak hrane koja se preuzima."
    /// </summary>
    public List<PickupItemDto> Items { get; set; } = [];
}

public class PickupItemDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DietaryTag DietaryTags { get; set; }
}
