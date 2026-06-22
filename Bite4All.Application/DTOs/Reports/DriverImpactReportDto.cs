namespace Bite4All.Application.DTOs.Reports;

public class DriverImpactReportDto
{
    public int DriverId { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public int CompletedPickups { get; set; }
    public decimal TransportedKg { get; set; }
    public int CancellationCount { get; set; }
    public double ReputationScore { get; set; }
    public string NextBadgeHint { get; set; } = string.Empty;
    public List<ReputationHistoryDto> ReputationHistory { get; set; } = [];
    public List<BadgeSummaryDto> Badges { get; set; } = [];
}
