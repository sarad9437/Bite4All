namespace Bite4All.Application.DTOs.Pickups;

public class ResolvePickupIssueRequest
{
    /// <summary>
    /// When true the pickup is cancelled; when false it resumes from ProblemReported.
    /// </summary>
    public bool Cancel { get; set; }
}
