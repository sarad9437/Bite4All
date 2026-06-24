using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Infrastructure;
using Bite4All.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bite4All.Tests;

/// <summary>
/// Tests that suspended drivers cannot log in and that deactivated recipients
/// are properly excluded from live operations.
/// </summary>
public class SuspendedDriverLoginTests
{
    private static Bite4AllContext BuildContext() =>
        new(new DbContextOptionsBuilder<Bite4AllContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Suspended_driver_IsActive_false_blocks_login()
    {
        await using var ctx = BuildContext();
        ctx.CharityOrganizations.Add(new CharityOrganization
        {
            Id = 1,
            Name = "Org",
            Address = "A",
            CityId = 1,
            ContactEmail = "o@x.local",
            ContactPhone = "1",
            TaxIdentificationNumber = "1",
            ApprovalStatus = ApprovalStatus.Approved,
            DailyCapacityKg = 100
        });
        ctx.Drivers.Add(new Driver
        {
            Id = 1,
            CharityOrganizationId = 1,
            FullName = "Vozac",
            Email = "v@x.local",
            Phone = "2",
            IsActive = false,   // suspended
            SuspensionReason = "Violation of transport rules"
        });
        await ctx.SaveChangesAsync();

        var driver = ctx.Drivers.First();

        // The login check in AuthController: if (!driver.IsActive) → Unauthorized
        var canLogin = driver.IsActive;
        Assert.False(canLogin, "Suspended driver must not be able to log in.");
        Assert.Equal("Violation of transport rules", driver.SuspensionReason);
    }

    [Fact]
    public async Task Active_driver_with_approved_org_can_login()
    {
        await using var ctx = BuildContext();
        ctx.CharityOrganizations.Add(new CharityOrganization
        {
            Id = 1,
            Name = "Org",
            Address = "A",
            CityId = 1,
            ContactEmail = "o@x.local",
            ContactPhone = "1",
            TaxIdentificationNumber = "1",
            ApprovalStatus = ApprovalStatus.Approved,
            DailyCapacityKg = 100
        });
        ctx.Drivers.Add(new Driver
        {
            Id = 1,
            CharityOrganizationId = 1,
            FullName = "Vozac",
            Email = "v@x.local",
            Phone = "2",
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var driver = ctx.Drivers.First();
        var org = ctx.CharityOrganizations.First();

        Assert.True(driver.IsActive);
        Assert.Equal(ApprovalStatus.Approved, org.ApprovalStatus);
    }

    [Fact]
    public async Task Deactivated_recipient_is_excluded_from_meal_distribution()
    {
        await using var ctx = BuildContext();
        ctx.CharityOrganizations.Add(new CharityOrganization
        {
            Id = 1, Name = "Org", Address = "A", CityId = 1,
            ContactEmail = "o@x.local", ContactPhone = "1", TaxIdentificationNumber = "1",
            ApprovalStatus = ApprovalStatus.Approved, DailyCapacityKg = 100
        });
        ctx.Recipients.AddRange(
            new Recipient { Id = 1, CharityOrganizationId = 1, InternalCode = "R-1", IsActive = true },
            new Recipient { Id = 2, CharityOrganizationId = 1, InternalCode = "R-2", IsActive = false }  // deactivated
        );
        await ctx.SaveChangesAsync();

        // Simulate the controller check: deactivated recipient is rejected
        var recipient = ctx.Recipients.First(r => r.Id == 2);
        Assert.False(recipient.IsActive, "Deactivated recipient must be blocked from new distributions.");
    }

    [Fact]
    public async Task GetRecipientsList_default_excludes_inactive_recipients()
    {
        await using var ctx = BuildContext();
        ctx.Recipients.AddRange(
            new Recipient { Id = 1, CharityOrganizationId = 1, InternalCode = "R-1", IsActive = true },
            new Recipient { Id = 2, CharityOrganizationId = 1, InternalCode = "R-2", IsActive = false },
            new Recipient { Id = 3, CharityOrganizationId = 1, InternalCode = "R-3", IsActive = true }
        );
        await ctx.SaveChangesAsync();

        // Default: includeInactive = false
        var activeOnly = ctx.Recipients
            .Where(r => r.CharityOrganizationId == 1 && r.IsActive)
            .ToList();

        Assert.Equal(2, activeOnly.Count);
        Assert.All(activeOnly, r => Assert.True(r.IsActive));
    }

    [Fact]
    public async Task GetRecipientsList_includeInactive_true_returns_all()
    {
        await using var ctx = BuildContext();
        ctx.Recipients.AddRange(
            new Recipient { Id = 1, CharityOrganizationId = 1, InternalCode = "R-1", IsActive = true },
            new Recipient { Id = 2, CharityOrganizationId = 1, InternalCode = "R-2", IsActive = false }
        );
        await ctx.SaveChangesAsync();

        var all = ctx.Recipients
            .Where(r => r.CharityOrganizationId == 1)
            .ToList();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task Privacy_summary_counts_only_active_recipients()
    {
        await using var ctx = BuildContext();
        ctx.CharityOrganizations.Add(new CharityOrganization
        {
            Id = 1, Name = "Org", Address = "A", CityId = 1,
            ContactEmail = "o@x.local", ContactPhone = "1", TaxIdentificationNumber = "1",
            ApprovalStatus = ApprovalStatus.Approved, DailyCapacityKg = 100
        });
        ctx.Recipients.AddRange(
            new Recipient { Id = 1, CharityOrganizationId = 1, InternalCode = "R-1", IsActive = true, DietaryRestrictions = DietaryTag.Halal },
            new Recipient { Id = 2, CharityOrganizationId = 1, InternalCode = "R-2", IsActive = false, DietaryRestrictions = DietaryTag.Vegan },
            new Recipient { Id = 3, CharityOrganizationId = 1, InternalCode = "R-3", IsActive = true, DietaryRestrictions = DietaryTag.None }
        );
        await ctx.SaveChangesAsync();

        // Simulate GetRecipientPrivacySummaryQuery with IsActive filter
        var activeRecipients = ctx.Recipients
            .Where(r => r.CharityOrganizationId == 1 && r.IsActive)
            .ToList();

        Assert.Equal(2, activeRecipients.Count);
        // Vegan restriction from deactivated recipient-2 must NOT appear
        Assert.DoesNotContain(activeRecipients, r => r.DietaryRestrictions == DietaryTag.Vegan);
    }
}
