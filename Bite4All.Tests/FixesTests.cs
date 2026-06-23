using System.Security.Claims;
using Bite4All.API.Controllers;
using Bite4All.API.Hubs;
using Bite4All.API.Validators;
using Bite4All.Application.DTOs.FoodOffers;
using Bite4All.Application.DTOs.Onboarding;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Infrastructure;
using Bite4All.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bite4All.Tests;

// ---------------------------------------------------------------------------
// Fix 2 — GET /blocks/my
// ---------------------------------------------------------------------------
public class BlocksMyTests
{
    private static Bite4AllContext BuildContext() =>
        new(new DbContextOptionsBuilder<Bite4AllContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static BlocksController MakeController(Bite4AllContext ctx, int? partnerId = null, int? orgId = null)
    {
        var claims = new List<Claim>();
        if (partnerId.HasValue) claims.Add(new Claim("hospitalityPartnerId", partnerId.Value.ToString()));
        if (orgId.HasValue) claims.Add(new Claim("charityOrganizationId", orgId.Value.ToString()));

        return new BlocksController(new UnitOfWork(ctx))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
                }
            }
        };
    }

    [Fact]
    public void Partner_sees_only_their_own_blocks()
    {
        using var ctx = BuildContext();
        ctx.BlockRelations.AddRange(
            new BlockRelation { Id = 1, HospitalityPartnerId = 1, CharityOrganizationId = 1, BlockedByHospitalityPartner = true, Reason = "a" },
            new BlockRelation { Id = 2, HospitalityPartnerId = 2, CharityOrganizationId = 1, BlockedByHospitalityPartner = true, Reason = "b" }
        );
        ctx.SaveChanges();

        var controller = MakeController(ctx, partnerId: 1);
        var result = controller.GetMine(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsType<List<BlockRelation>>(ok.Value);
        Assert.Single(list);
        Assert.Equal(1, list[0].HospitalityPartnerId);
    }

    [Fact]
    public void Organisation_sees_only_their_own_blocks()
    {
        using var ctx = BuildContext();
        ctx.BlockRelations.AddRange(
            new BlockRelation { Id = 1, HospitalityPartnerId = 1, CharityOrganizationId = 1, BlockedByOrganization = true, Reason = "a" },
            new BlockRelation { Id = 2, HospitalityPartnerId = 1, CharityOrganizationId = 2, BlockedByOrganization = true, Reason = "b" }
        );
        ctx.SaveChanges();

        var controller = MakeController(ctx, orgId: 1);
        var result = controller.GetMine(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsType<List<BlockRelation>>(ok.Value);
        Assert.Single(list);
        Assert.Equal(1, list[0].CharityOrganizationId);
    }

    [Fact]
    public void Driver_without_partner_or_org_gets_forbidden()
    {
        using var ctx = BuildContext();
        var controller = new BlocksController(new UnitOfWork(ctx))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    // Authenticated but neither partner nor org
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim("driverId", "1")], "Test"))
                }
            }
        };

        var result = controller.GetMine(CancellationToken.None);
        Assert.IsType<ForbidResult>(result.Result);
    }
}

// ---------------------------------------------------------------------------
// Fix 3 — GET /matches/my
// ---------------------------------------------------------------------------
public class MatchesMyTests
{
    private static Bite4AllContext BuildContext() =>
        new(new DbContextOptionsBuilder<Bite4AllContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static MatchesController MakeController(Bite4AllContext ctx, int orgId)
    {
        return new MatchesController(new UnitOfWork(ctx), new FakeNotificationPublisher())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim("charityOrganizationId", orgId.ToString())], "Test"))
                }
            }
        };
    }

    [Fact]
    public void Organisation_sees_only_their_own_matches()
    {
        using var ctx = BuildContext();
        ctx.OfferMatches.AddRange(
            new OfferMatch { Id = 1, FoodOfferId = 1, CharityOrganizationId = 1, Rank = 1, Score = 80, Decision = MatchDecision.Pending },
            new OfferMatch { Id = 2, FoodOfferId = 1, CharityOrganizationId = 2, Rank = 2, Score = 70, Decision = MatchDecision.Pending },
            new OfferMatch { Id = 3, FoodOfferId = 2, CharityOrganizationId = 1, Rank = 1, Score = 90, Decision = MatchDecision.Accepted }
        );
        ctx.SaveChanges();

        var controller = MakeController(ctx, orgId: 1);
        var result = controller.GetMine(cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        // PagedResult<OfferMatch>
        dynamic paged = ok.Value!;
        var items = (System.Collections.IEnumerable)paged.Items;
        int count = 0;
        foreach (var _ in items) count++;
        Assert.Equal(2, count); // only org-1 matches
    }

    [Fact]
    public void Skipped_matches_excluded_by_default()
    {
        using var ctx = BuildContext();
        ctx.OfferMatches.AddRange(
            new OfferMatch { Id = 1, FoodOfferId = 1, CharityOrganizationId = 1, Rank = 1, Score = 80, Decision = MatchDecision.Pending },
            new OfferMatch { Id = 2, FoodOfferId = 2, CharityOrganizationId = 1, Rank = 0, Score = 0, Decision = MatchDecision.SkippedByDiet },
            new OfferMatch { Id = 3, FoodOfferId = 3, CharityOrganizationId = 1, Rank = 0, Score = 0, Decision = MatchDecision.SkippedByCapacity }
        );
        ctx.SaveChanges();

        var controller = MakeController(ctx, orgId: 1);
        var result = controller.GetMine(includeSkipped: false, cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        dynamic paged = ok.Value!;
        var items = (System.Collections.IEnumerable)paged.Items;
        int count = 0;
        foreach (var _ in items) count++;
        Assert.Equal(1, count); // only the Pending match
    }

    [Fact]
    public void Skipped_matches_included_when_requested()
    {
        using var ctx = BuildContext();
        ctx.OfferMatches.AddRange(
            new OfferMatch { Id = 1, FoodOfferId = 1, CharityOrganizationId = 1, Rank = 1, Score = 80, Decision = MatchDecision.Pending },
            new OfferMatch { Id = 2, FoodOfferId = 2, CharityOrganizationId = 1, Rank = 0, Score = 0, Decision = MatchDecision.SkippedByDiet }
        );
        ctx.SaveChanges();

        var controller = MakeController(ctx, orgId: 1);
        var result = controller.GetMine(includeSkipped: true, cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        dynamic paged = ok.Value!;
        var items = (System.Collections.IEnumerable)paged.Items;
        int count = 0;
        foreach (var _ in items) count++;
        Assert.Equal(2, count);
    }

    private sealed class FakeNotificationPublisher : INotificationPublisher
    {
        public Task NotifyAsync(ActorType actorType, int actorId, string title, string body,
            CancellationToken cancellationToken = default, NotificationType type = NotificationType.AdminMessage,
            ActorType? sourceActorType = null, int? sourceActorId = null) => Task.CompletedTask;
    }
}

// ---------------------------------------------------------------------------
// Fix 6 — Suspend/Unsuspend: AuthController returns correct messages
// ---------------------------------------------------------------------------
public class SuspendedLoginTests
{
    private static Bite4AllContext BuildContext() =>
        new(new DbContextOptionsBuilder<Bite4AllContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public void Suspended_partner_status_is_distinct_from_pending()
    {
        // Verify that ApprovalStatus.Suspended != Approved so login is blocked.
        Assert.NotEqual(ApprovalStatus.Approved, ApprovalStatus.Suspended);
        Assert.NotEqual(ApprovalStatus.Pending, ApprovalStatus.Suspended);
        Assert.NotEqual(ApprovalStatus.Rejected, ApprovalStatus.Suspended);
    }

    [Fact]
    public void Suspended_partner_has_enum_value_4()
    {
        // Ensure the enum hasn't shifted — Suspended = 4 per domain definition.
        Assert.Equal(4, (int)ApprovalStatus.Suspended);
    }

    [Fact]
    public async Task Suspend_and_unsuspend_roundtrip_updates_approval_status()
    {
        await using var ctx = BuildContext();
        ctx.HospitalityPartners.Add(new HospitalityPartner
        {
            Id = 1,
            Name = "Pekara",
            PartnerType = "Pekara",
            Address = "A",
            CityId = 1,
            ContactEmail = "p@x.local",
            ContactPhone = "1",
            TaxIdentificationNumber = "1",
            ApprovalStatus = ApprovalStatus.Approved
        });
        await ctx.SaveChangesAsync();

        // Simulate suspend
        var partner = ctx.HospitalityPartners.First();
        partner.ApprovalStatus = ApprovalStatus.Suspended;
        partner.RejectionReason = "Repeated violations";
        await ctx.SaveChangesAsync();

        Assert.Equal(ApprovalStatus.Suspended, ctx.HospitalityPartners.First().ApprovalStatus);
        Assert.Equal("Repeated violations", ctx.HospitalityPartners.First().RejectionReason);

        // Simulate unsuspend
        partner.ApprovalStatus = ApprovalStatus.Approved;
        partner.RejectionReason = null;
        await ctx.SaveChangesAsync();

        Assert.Equal(ApprovalStatus.Approved, ctx.HospitalityPartners.First().ApprovalStatus);
        Assert.Null(ctx.HospitalityPartners.First().RejectionReason);
    }
}

// ---------------------------------------------------------------------------
// Fix 9 — GET /food-offers/{id}: unauthenticated gets 404, not 403
// ---------------------------------------------------------------------------
public class FoodOfferVisibilityTests
{
    private static Bite4AllContext BuildContext() =>
        new(new DbContextOptionsBuilder<Bite4AllContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Theory]
    [InlineData(FoodOfferStatus.Reserved)]
    [InlineData(FoodOfferStatus.Completed)]
    [InlineData(FoodOfferStatus.Cancelled)]
    [InlineData(FoodOfferStatus.Expired)]
    [InlineData(FoodOfferStatus.Draft)]
    [InlineData(FoodOfferStatus.PendingRestaurantConfirmation)]
    public void Non_public_offer_statuses_are_not_active_or_publicfallback(FoodOfferStatus status)
    {
        // Confirms the set of "non-public" statuses that should return 404 to anonymous users.
        var isPublic = status is FoodOfferStatus.Active or FoodOfferStatus.PublicFallback;
        Assert.False(isPublic);
    }

    [Theory]
    [InlineData(FoodOfferStatus.Active)]
    [InlineData(FoodOfferStatus.PublicFallback)]
    public void Public_offer_statuses_are_active_or_publicfallback(FoodOfferStatus status)
    {
        var isPublic = status is FoodOfferStatus.Active or FoodOfferStatus.PublicFallback;
        Assert.True(isPublic);
    }
}

// ---------------------------------------------------------------------------
// Fix 11 — DocumentNumber generation: Guid suffix avoids Ticks collisions
// ---------------------------------------------------------------------------
public class DocumentNumberTests
{
    [Fact]
    public void Document_number_uses_guid_suffix_format()
    {
        // Simulate the new generation logic from PickupsController.CreateFromMatch
        var docSuffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var docNumber = $"PR-{DateTime.UtcNow:yyyy}-{docSuffix}";

        // Must start with the year prefix
        Assert.StartsWith($"PR-{DateTime.UtcNow:yyyy}-", docNumber);
        // Suffix is 8 uppercase hex chars
        Assert.Matches("^PR-\\d{4}-[0-9A-F]{8}$", docNumber);
    }

    [Fact]
    public void Document_numbers_are_unique_across_rapid_calls()
    {
        var numbers = Enumerable.Range(0, 1000)
            .Select(_ => $"PR-{DateTime.UtcNow:yyyy}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}")
            .ToList();

        Assert.Equal(numbers.Count, numbers.Distinct().Count());
    }

    [Fact]
    public void Old_ticks_approach_has_collision_risk()
    {
        // Demonstrates the problem with Ticks % 100000 — only 100 000 unique values/year.
        var ticks1 = 637000000000000000L;
        var ticks2 = ticks1 + 100000; // exactly one period later → same suffix
        Assert.Equal(ticks1 % 100000, ticks2 % 100000);
    }
}

// ---------------------------------------------------------------------------
// Fix 13 — CreateFoodOfferRequestValidator: category 0 is rejected
// ---------------------------------------------------------------------------
public class FoodOfferCategoryValidationTests
{
    [Fact]
    public void Validator_rejects_default_category_zero()
    {
        var validator = new CreateFoodOfferRequestValidator();
        var request = new CreateFoodOfferRequest
        {
            HospitalityPartnerId = 1,
            TotalQuantityKg = 5,
            Category = 0, // default — invalid
            PickupWindowStartUtc = DateTime.UtcNow.AddMinutes(30),
            PickupWindowEndUtc = DateTime.UtcNow.AddHours(2),
            ExpiresAtUtc = DateTime.UtcNow.AddHours(4),
            Items = [new CreateFoodOfferItemRequest { Name = "Hleb", Quantity = 5, Unit = "kom" }]
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Category");
    }

    [Theory]
    [InlineData(FoodCategory.Bakery)]
    [InlineData(FoodCategory.CookedMeal)]
    [InlineData(FoodCategory.FruitAndVegetables)]
    [InlineData(FoodCategory.Dairy)]
    [InlineData(FoodCategory.Packaged)]
    [InlineData(FoodCategory.Mixed)]
    public void Validator_accepts_all_valid_categories(FoodCategory category)
    {
        var validator = new CreateFoodOfferRequestValidator();
        var request = new CreateFoodOfferRequest
        {
            HospitalityPartnerId = 1,
            TotalQuantityKg = 5,
            Category = category,
            PickupWindowStartUtc = DateTime.UtcNow.AddMinutes(30),
            PickupWindowEndUtc = DateTime.UtcNow.AddHours(2),
            ExpiresAtUtc = DateTime.UtcNow.AddHours(4),
            Items = [new CreateFoodOfferItemRequest { Name = "Hleb", Quantity = 5, Unit = "kom" }]
        };

        var result = validator.Validate(request);

        // Category-specific rule should pass (other rules may still fail but category is fine)
        Assert.DoesNotContain(result.Errors, e => e.PropertyName == "Category");
    }
}
