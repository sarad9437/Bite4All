namespace Bite4All.Domain.Enums;

public enum MatchDecision
{
    Pending = 1,
    Accepted = 2,
    Declined = 3,
    TimedOut = 4,
    SkippedByBlock = 5,
    SkippedByCapacity = 6,
    SkippedByDiet = 7,
    Cancelled = 8
}
