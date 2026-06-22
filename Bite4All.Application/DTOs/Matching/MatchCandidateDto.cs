using Bite4All.Domain.Enums;

namespace Bite4All.Application.DTOs.Matching;

public class MatchCandidateDto
{
    public int OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public int Rank { get; set; }
    public MatchDecision Decision { get; set; }
}
