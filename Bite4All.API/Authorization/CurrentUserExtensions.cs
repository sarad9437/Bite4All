using System.Security.Claims;

namespace Bite4All.API.Authorization;

public static class CurrentUserExtensions
{
    public static bool IsAdministrator(this ClaimsPrincipal user)
    {
        return user.IsInRole("Administrator");
    }

    public static int? HospitalityPartnerId(this ClaimsPrincipal user)
    {
        return ReadIntClaim(user, "hospitalityPartnerId");
    }

    public static int? CharityOrganizationId(this ClaimsPrincipal user)
    {
        return ReadIntClaim(user, "charityOrganizationId");
    }

    public static int? DriverId(this ClaimsPrincipal user)
    {
        return ReadIntClaim(user, "driverId");
    }

    private static int? ReadIntClaim(ClaimsPrincipal user, string name)
    {
        var value = user.FindFirstValue(name);
        return int.TryParse(value, out var id) ? id : null;
    }
}
