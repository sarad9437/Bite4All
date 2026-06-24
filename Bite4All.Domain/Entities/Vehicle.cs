using Bite4All.Domain.Common;

namespace Bite4All.Domain.Entities;

public class Vehicle : Entity
{
    public int CharityOrganizationId { get; set; }
    public CharityOrganization? CharityOrganization { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public decimal CapacityKg { get; set; }
    public bool HasCooling { get; set; }
    /// <summary>
    /// Operational availability — false while the vehicle is assigned to an
    /// active pickup. Reset to true when the pickup is completed or cancelled.
    /// </summary>
    public bool IsAvailable { get; set; } = true;
    /// <summary>
    /// Soft-delete / permanent deactivation flag. Set to false when a vehicle
    /// is retired or sold. Deactivated vehicles cannot be assigned to new
    /// pickups but their history is preserved.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
