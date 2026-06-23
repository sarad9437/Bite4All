namespace Bite4All.Application.DTOs.Onboarding;

public class SuspendRequest
{
    /// <summary>
    /// Human-readable reason for the suspension shown to the suspended actor.
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}
