using Bite4All.Domain.Common;

namespace Bite4All.Domain.Entities;

public class Driver : Entity
{
    public int CharityOrganizationId { get; set; }
    public CharityOrganization? CharityOrganization { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = true;
    /// <summary>
    /// False when the driver has been suspended by their organization or an admin.
    /// A suspended driver cannot log in and will not be available for new pickups.
    /// Unlike IsAvailable (which is a per-pickup operational flag), IsActive is a
    /// permanent account-level flag that persists across pickups.
    /// </summary>
    public bool IsActive { get; set; } = true;
    /// <summary>
    /// Reason stored when the driver account is suspended or deactivated.
    /// Shown to the driver on login attempt.
    /// </summary>
    public string? SuspensionReason { get; set; }
    public decimal? CurrentLatitude { get; set; }
    public decimal? CurrentLongitude { get; set; }
    public DateTime? LocationUpdatedAtUtc { get; set; }
    public double ReputationScore { get; set; } = 3.5;
    public int CompletedPickups { get; set; }
    public int CancellationCount { get; set; }
}
