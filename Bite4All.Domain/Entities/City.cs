using Bite4All.Domain.Common;

namespace Bite4All.Domain.Entities;

public class City : Entity
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
