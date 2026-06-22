using Bite4All.Domain.Enums;

namespace Bite4All.Application.DTOs.Pickups;

public class CreatePickupIssueRequest
{
    public PickupIssueType IssueType { get; set; }
    public string Note { get; set; } = string.Empty;
}
