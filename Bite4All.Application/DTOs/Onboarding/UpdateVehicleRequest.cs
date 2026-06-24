namespace Bite4All.Application.DTOs.Onboarding;

public class UpdateVehicleRequest
{
    public string? RegistrationNumber { get; set; }
    public decimal? CapacityKg { get; set; }
    public bool? HasCooling { get; set; }
}
