namespace Bite4All.Application.DTOs.Onboarding;

public class CreateVehicleRequest
{
    public int CharityOrganizationId { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public decimal CapacityKg { get; set; }
    public bool HasCooling { get; set; }
}
