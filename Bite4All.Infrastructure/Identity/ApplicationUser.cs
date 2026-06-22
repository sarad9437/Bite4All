using Bite4All.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace Bite4All.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public ActorType ActorType { get; set; }
    public int? HospitalityPartnerId { get; set; }
    public int? CharityOrganizationId { get; set; }
    public int? DriverId { get; set; }
}
