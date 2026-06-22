namespace Bite4All.Application.DTOs.Onboarding;

public class UpdateTemporaryCapacityRequest
{
    public decimal TemporaryExtraCapacityKg { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}
