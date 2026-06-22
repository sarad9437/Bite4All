using Bite4All.Domain.Common;

namespace Bite4All.Domain.Entities;

public class Vehicle : Entity
{
    public int CharityOrganizationId { get; set; }
    public CharityOrganization? CharityOrganization { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public decimal CapacityKg { get; set; }
    public bool HasCooling { get; set; }
    public bool IsAvailable { get; set; } = true;
}
