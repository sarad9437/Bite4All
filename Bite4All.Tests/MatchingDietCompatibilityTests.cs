using Bite4All.Application.Services;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Infrastructure;
using Bite4All.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bite4All.Tests;

/// <summary>
/// Fix 7: dietary compatibility must use ALL-recipients logic.
/// An organisation is only eligible if the offered food satisfies
/// the restrictions of EVERY recipient, not just one of them.
/// </summary>
public class MatchingDietCompatibilityTests
{
    // -----------------------------------------------------------------------
    // helpers
    // -----------------------------------------------------------------------

    private static Bite4AllContext BuildContext() =>
        new(new DbContextOptionsBuilder<Bite4AllContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static HospitalityPartner SeedPartner(Bite4AllContext ctx, int id = 1)
    {
        var partner = new HospitalityPartner
        {
            Id = id,
            Name = "Pekara",
            PartnerType = "Pekara",
            Address = "Adresa",
            CityId = 1,
            ContactEmail = "p@x.local",
            ContactPhone = "1",
            TaxIdentificationNumber = $"TIN-{id}",
            ApprovalStatus = ApprovalStatus.Approved,
            Latitude = 44.8m,
            Longitude = 20.5m
        };
        ctx.HospitalityPartners.Add(partner);
        return partner;
    }

    private static CharityOrganization SeedOrganization(
        Bite4AllContext ctx,
        int id,
        decimal lat = 44.8m,
        decimal lon = 20.5m,
        decimal capacity = 200m)
    {
        var org = new CharityOrganization
        {
            Id = id,
            Name = $"Org-{id}",
            Address = "Adresa",
            CityId = 1,
            ContactEmail = $"o{id}@x.local",
            ContactPhone = $"{id}",
            TaxIdentificationNumber = $"ORG-{id}",
            ApprovalStatus = ApprovalStatus.Approved,
            Latitude = lat,
            Longitude = lon,
            DailyCapacityKg = capacity
        };
        ctx.CharityOrganizations.Add(org);
        return org;
    }

    private static FoodOffer SeedOffer(
        Bite4AllContext ctx,
        int id,
        int partnerId,
        DietaryTag itemTag,
        decimal qty = 5m)
    {
        var offer = new FoodOffer
        {
            Id = id,
            HospitalityPartnerId = partnerId,
            TotalQuantityKg = qty,
            Category = FoodCategory.Bakery,
            Status = FoodOfferStatus.Active,
            PickupWindowStartUtc = DateTime.UtcNow.AddHours(1),
            PickupWindowEndUtc = DateTime.UtcNow.AddHours(3),
            ExpiresAtUtc = DateTime.UtcNow.AddHours(5)
        };
        ctx.FoodOffers.Add(offer);
        ctx.FoodOfferItems.Add(new FoodOfferItem
        {
            FoodOfferId = id,
            Name = "Testna stavka",
            Quantity = qty,
            Unit = "kg",
            DietaryTags = itemTag
        });
        return offer;
    }

    // -----------------------------------------------------------------------
    // tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// Organisation has two recipients: one has GlutenFree restriction, one has none.
    /// Offer is NOT tagged GlutenFree.
    /// Fix 7 (All): should be SKIPPED — one recipient cannot eat the food.
    /// Old behaviour (Any): would have been included because one recipient has no restrictions.
    /// </summary>
    [Fact]
    public async Task Organisation_skipped_when_one_recipient_has_unmet_restriction()
    {
        await using var ctx = BuildContext();
        ctx.Cities.Add(new City { Id = 1, Name = "Beograd" });
        SeedPartner(ctx);
        var org = SeedOrganization(ctx, 1);
        // Recipient 1: no restrictions → fine
        ctx.Recipients.Add(new Recipient { Id = 1, CharityOrganizationId = 1, InternalCode = "R-1", DietaryRestrictions = DietaryTag.None });
        // Recipient 2: must have GlutenFree food → NOT satisfied by offer below
        ctx.Recipients.Add(new Recipient { Id = 2, CharityOrganizationId = 1, InternalCode = "R-2", DietaryRestrictions = DietaryTag.GlutenFree });

        // Offer has NO GlutenFree tag → recipient 2 cannot eat it
        SeedOffer(ctx, 1, 1, DietaryTag.Vegetarian);
        await ctx.SaveChangesAsync();

        var service = new MatchingService(new UnitOfWork(ctx));
        var matches = await service.GenerateMatchesAsync(1);

        // Organisation should be skipped by diet
        Assert.DoesNotContain(matches, m => m.OrganizationId == 1 && m.Rank > 0);

        var skipped = ctx.OfferMatches.FirstOrDefault(m => m.FoodOfferId == 1 && m.CharityOrganizationId == 1);
        Assert.NotNull(skipped);
        Assert.Equal(MatchDecision.SkippedByDiet, skipped.Decision);
    }

    /// <summary>
    /// Organisation has two recipients both with GlutenFree restriction.
    /// Offer IS tagged GlutenFree.
    /// All recipients satisfied → organisation should be INCLUDED.
    /// </summary>
    [Fact]
    public async Task Organisation_included_when_all_recipients_restrictions_are_met()
    {
        await using var ctx = BuildContext();
        ctx.Cities.Add(new City { Id = 1, Name = "Beograd" });
        SeedPartner(ctx);
        SeedOrganization(ctx, 1);
        ctx.Recipients.Add(new Recipient { Id = 1, CharityOrganizationId = 1, InternalCode = "R-1", DietaryRestrictions = DietaryTag.GlutenFree });
        ctx.Recipients.Add(new Recipient { Id = 2, CharityOrganizationId = 1, InternalCode = "R-2", DietaryRestrictions = DietaryTag.GlutenFree });

        // Offer is GlutenFree → both recipients satisfied
        SeedOffer(ctx, 1, 1, DietaryTag.GlutenFree);
        await ctx.SaveChangesAsync();

        var service = new MatchingService(new UnitOfWork(ctx));
        var matches = await service.GenerateMatchesAsync(1);

        Assert.Contains(matches, m => m.OrganizationId == 1 && m.Rank > 0);
    }

    /// <summary>
    /// Organisation has no recipients at all.
    /// Should be treated as compatible (no restrictions to violate).
    /// </summary>
    [Fact]
    public async Task Organisation_with_no_recipients_is_diet_compatible()
    {
        await using var ctx = BuildContext();
        ctx.Cities.Add(new City { Id = 1, Name = "Beograd" });
        SeedPartner(ctx);
        SeedOrganization(ctx, 1);
        // No recipients added

        SeedOffer(ctx, 1, 1, DietaryTag.Halal);
        await ctx.SaveChangesAsync();

        var service = new MatchingService(new UnitOfWork(ctx));
        var matches = await service.GenerateMatchesAsync(1);

        Assert.Contains(matches, m => m.OrganizationId == 1 && m.Rank > 0);
    }

    /// <summary>
    /// Offer has no dietary tags (None).
    /// Organisation has a recipient with a GlutenFree restriction.
    /// Conservative: offer makes no claims → cannot guarantee safety → skip.
    /// </summary>
    [Fact]
    public async Task Organisation_skipped_when_offer_has_no_tags_but_recipient_has_restriction()
    {
        await using var ctx = BuildContext();
        ctx.Cities.Add(new City { Id = 1, Name = "Beograd" });
        SeedPartner(ctx);
        SeedOrganization(ctx, 1);
        ctx.Recipients.Add(new Recipient { Id = 1, CharityOrganizationId = 1, InternalCode = "R-1", DietaryRestrictions = DietaryTag.GlutenFree });

        // Offer makes no dietary claims
        SeedOffer(ctx, 1, 1, DietaryTag.None);
        await ctx.SaveChangesAsync();

        var service = new MatchingService(new UnitOfWork(ctx));
        var matches = await service.GenerateMatchesAsync(1);

        var skipped = ctx.OfferMatches.FirstOrDefault(m => m.FoodOfferId == 1 && m.CharityOrganizationId == 1);
        Assert.NotNull(skipped);
        Assert.Equal(MatchDecision.SkippedByDiet, skipped.Decision);
    }

    /// <summary>
    /// Offer has no dietary tags AND organisation has no restricted recipients.
    /// No restrictions to violate → should be included.
    /// </summary>
    [Fact]
    public async Task Organisation_included_when_offer_no_tags_and_recipients_have_no_restrictions()
    {
        await using var ctx = BuildContext();
        ctx.Cities.Add(new City { Id = 1, Name = "Beograd" });
        SeedPartner(ctx);
        SeedOrganization(ctx, 1);
        ctx.Recipients.Add(new Recipient { Id = 1, CharityOrganizationId = 1, InternalCode = "R-1", DietaryRestrictions = DietaryTag.None });

        SeedOffer(ctx, 1, 1, DietaryTag.None);
        await ctx.SaveChangesAsync();

        var service = new MatchingService(new UnitOfWork(ctx));
        var matches = await service.GenerateMatchesAsync(1);

        Assert.Contains(matches, m => m.OrganizationId == 1 && m.Rank > 0);
    }

    /// <summary>
    /// Recipient has multiple restrictions (GlutenFree | Halal).
    /// Offer satisfies both → should be included.
    /// </summary>
    [Fact]
    public async Task Organisation_included_when_offer_satisfies_multiple_restrictions()
    {
        await using var ctx = BuildContext();
        ctx.Cities.Add(new City { Id = 1, Name = "Beograd" });
        SeedPartner(ctx);
        SeedOrganization(ctx, 1);
        ctx.Recipients.Add(new Recipient
        {
            Id = 1,
            CharityOrganizationId = 1,
            InternalCode = "R-1",
            DietaryRestrictions = DietaryTag.GlutenFree | DietaryTag.Halal
        });

        // Offer satisfies both GlutenFree and Halal
        SeedOffer(ctx, 1, 1, DietaryTag.GlutenFree | DietaryTag.Halal | DietaryTag.Vegetarian);
        await ctx.SaveChangesAsync();

        var service = new MatchingService(new UnitOfWork(ctx));
        var matches = await service.GenerateMatchesAsync(1);

        Assert.Contains(matches, m => m.OrganizationId == 1 && m.Rank > 0);
    }

    /// <summary>
    /// Recipient has combined restriction (GlutenFree | Halal).
    /// Offer satisfies only GlutenFree but NOT Halal → skip.
    /// </summary>
    [Fact]
    public async Task Organisation_skipped_when_offer_satisfies_only_part_of_combined_restriction()
    {
        await using var ctx = BuildContext();
        ctx.Cities.Add(new City { Id = 1, Name = "Beograd" });
        SeedPartner(ctx);
        SeedOrganization(ctx, 1);
        ctx.Recipients.Add(new Recipient
        {
            Id = 1,
            CharityOrganizationId = 1,
            InternalCode = "R-1",
            DietaryRestrictions = DietaryTag.GlutenFree | DietaryTag.Halal
        });

        // Offer is GlutenFree but NOT Halal
        SeedOffer(ctx, 1, 1, DietaryTag.GlutenFree);
        await ctx.SaveChangesAsync();

        var service = new MatchingService(new UnitOfWork(ctx));
        var matches = await service.GenerateMatchesAsync(1);

        var skipped = ctx.OfferMatches.FirstOrDefault(m => m.FoodOfferId == 1 && m.CharityOrganizationId == 1);
        Assert.NotNull(skipped);
        Assert.Equal(MatchDecision.SkippedByDiet, skipped.Decision);
    }

    /// <summary>
    /// Two organisations: org-1 has a GlutenFree restriction that is NOT met,
    /// org-2 has no restrictions.
    /// Only org-2 should appear in the ranked list.
    /// </summary>
    [Fact]
    public async Task Only_compatible_organisations_appear_in_ranked_results()
    {
        await using var ctx = BuildContext();
        ctx.Cities.Add(new City { Id = 1, Name = "Beograd" });
        SeedPartner(ctx);

        // Org-1: has restricted recipient, offer won't satisfy it
        SeedOrganization(ctx, 1);
        ctx.Recipients.Add(new Recipient { Id = 1, CharityOrganizationId = 1, InternalCode = "R-1", DietaryRestrictions = DietaryTag.GlutenFree });

        // Org-2: no restrictions
        SeedOrganization(ctx, 2, lat: 44.81m, lon: 20.51m);
        ctx.Recipients.Add(new Recipient { Id = 2, CharityOrganizationId = 2, InternalCode = "R-2", DietaryRestrictions = DietaryTag.None });

        // Offer is NOT GlutenFree
        SeedOffer(ctx, 1, 1, DietaryTag.Vegetarian);
        await ctx.SaveChangesAsync();

        var service = new MatchingService(new UnitOfWork(ctx));
        var matches = await service.GenerateMatchesAsync(1);

        // Only org-2 should be ranked
        Assert.Single(matches);
        Assert.Equal(2, matches[0].OrganizationId);

        // Org-1 must be recorded as skipped by diet
        var skipped = ctx.OfferMatches.FirstOrDefault(m => m.CharityOrganizationId == 1);
        Assert.NotNull(skipped);
        Assert.Equal(MatchDecision.SkippedByDiet, skipped.Decision);
    }
}
