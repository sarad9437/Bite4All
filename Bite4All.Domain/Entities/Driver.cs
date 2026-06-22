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
    public decimal? CurrentLatitude { get; set; }
    public decimal? CurrentLongitude { get; set; }
    public DateTime? LocationUpdatedAtUtc { get; set; }
    public double ReputationScore { get; set; } = 3.5;
    public int CompletedPickups { get; set; }
    public int CancellationCount { get; set; }
}
