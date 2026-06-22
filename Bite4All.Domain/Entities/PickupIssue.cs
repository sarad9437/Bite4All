using Bite4All.Domain.Common;
using Bite4All.Domain.Enums;

namespace Bite4All.Domain.Entities;

public class PickupIssue : Entity
{
    public int PickupDocumentId { get; set; }
    public PickupDocument? PickupDocument { get; set; }
    public PickupIssueType IssueType { get; set; }
    public string Note { get; set; } = string.Empty;
}
