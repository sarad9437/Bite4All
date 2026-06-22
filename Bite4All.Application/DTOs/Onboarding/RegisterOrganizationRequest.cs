namespace Bite4All.Application.DTOs.Onboarding;

public class RegisterOrganizationRequest
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int CityId { get; set; }
    public string ContactEmail { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string TaxIdentificationNumber { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal DailyCapacityKg { get; set; }
}
