using Bite4All.Application.DTOs.Matching;

namespace Bite4All.Application.Services;

public interface IMatchingService
{
    Task<List<MatchCandidateDto>> GenerateMatchesAsync(int foodOfferId, CancellationToken cancellationToken = default);
}
