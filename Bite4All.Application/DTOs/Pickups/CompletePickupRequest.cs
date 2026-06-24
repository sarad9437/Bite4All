namespace Bite4All.Application.DTOs.Pickups;

public class CompletePickupRequest
{
    /// <summary>
    /// Stvarna preuzeta količina u kilogramima. Mora biti veća od nule.
    /// Može biti manja od planirane ako restoran nije imao punu količinu.
    /// </summary>
    public decimal ActualQuantityKg { get; set; }
    public string? DriverNote { get; set; }
}
