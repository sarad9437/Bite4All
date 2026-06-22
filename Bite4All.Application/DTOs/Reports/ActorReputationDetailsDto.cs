using Bite4All.Domain.Enums;

namespace Bite4All.Application.DTOs.Reports;

public class ActorReputationDetailsDto
{
    public ActorType ActorType { get; set; }
    public int ActorId { get; set; }
    public double ReputationScore { get; set; }
    public int SuccessfulDonations { get; set; }
    public int CancellationCount { get; set; }
    public List<BadgeSummaryDto> Badges { get; set; } = [];
    public List<ReputationHistoryDto> History { get; set; } = [];
}

public class BadgeSummaryDto
{
    public string Name { get; set; } = string.Empty;
    public BadgeLevel Level { get; set; }
    public bool AssignedByAdmin { get; set; }
}

public class ReputationHistoryDto
{
    public DateTime RecordedAtUtc { get; set; }
    public double Score { get; set; }
    public string Source { get; set; } = string.Empty;
}
