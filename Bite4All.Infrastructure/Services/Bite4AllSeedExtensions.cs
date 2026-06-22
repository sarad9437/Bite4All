using Bite4All.Domain.Enums;
using Bite4All.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bite4All.Infrastructure.Services;

public static class Bite4AllSeedExtensions
{
    public static async Task SeedIdentityAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Bite4AllContext>();
        await context.Database.EnsureCreatedAsync();

        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in new[] { "Administrator", "HospitalityPartner", "CharityOrganization", "Driver" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var seedUsers = configuration.GetSection("SeedUsers");
        await EnsureUserAsync(userManager, "admin@bite4all.local", seedUsers["AdminPassword"], "Administrator", ActorType.Administrator, null, null, null);
        await EnsureUserAsync(userManager, "pekara@bite4all.local", seedUsers["PartnerPassword"], "HospitalityPartner", ActorType.HospitalityPartner, 1, null, null);
        await EnsureUserAsync(userManager, "kuhinja@bite4all.local", seedUsers["OrganizationPassword"], "CharityOrganization", ActorType.CharityOrganization, null, 1, null);
        await EnsureUserAsync(userManager, "vozac@bite4all.local", seedUsers["DriverPassword"], "Driver", ActorType.Driver, null, 1, 1);
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string? password,
        string role,
        ActorType actorType,
        int? hospitalityPartnerId,
        int? charityOrganizationId,
        int? driverId)
    {
        if (!IsConfiguredPassword(password))
        {
            return;
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                ActorType = actorType,
                HospitalityPartnerId = hospitalityPartnerId,
                CharityOrganizationId = charityOrganizationId,
                DriverId = driverId
            };
            await userManager.CreateAsync(user, password);
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }

    private static bool IsConfiguredPassword(string? password)
    {
        return !string.IsNullOrWhiteSpace(password) &&
               !password.StartsWith("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase);
    }
}
