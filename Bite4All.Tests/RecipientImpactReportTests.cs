using Bite4All.Application.Services;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Infrastructure;
using Bite4All.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bite4All.Tests;

public class RecipientImpactReportTests
{
    [Fact]
    public async Task Organization_report_includes_recorded_meals_and_unique_recipients()
    {
        var options = new DbContextOptionsBuilder<Bite4AllContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new Bite4AllContext(options);
        context.CharityOrganizations.Add(new CharityOrganization
        {
            Id = 1,
            Name = "Organizacija",
            Address = "Adresa",
            CityId = 1,
            ContactEmail = "o@x.local",
            ContactPhone = "2",
            TaxIdentificationNumber = "2",
            ApprovalStatus = ApprovalStatus.Approved,
            DailyCapacityKg = 100
        });
        context.FoodOffers.Add(new FoodOffer
        {
            Id = 1,
            HospitalityPartnerId = 1,
            TotalQuantityKg = 10,
            Category = FoodCategory.Bakery,
            PickupWindowStartUtc = DateTime.UtcNow,
            PickupWindowEndUtc = DateTime.UtcNow.AddHours(1),
            ExpiresAtUtc = DateTime.UtcNow.AddHours(2)
        });
        context.PickupDocuments.Add(new PickupDocument
        {
            Id = 1,
            DocumentNumber = "PR-2026-00002",
            FoodOfferId = 1,
            HospitalityPartnerId = 1,
            CharityOrganizationId = 1,
            PlannedQuantityKg = 10,
            ActualQuantityKg = 9,
            Status = PickupStatus.PickedUp
        });
        context.Recipients.AddRange(
            new Recipient { Id = 1, CharityOrganizationId = 1, InternalCode = "R-1" },
            new Recipient { Id = 2, CharityOrganizationId = 1, InternalCode = "R-2" });
        context.RecipientMealDistributions.AddRange(
            new RecipientMealDistribution { PickupDocumentId = 1, RecipientId = 1, CharityOrganizationId = 1, Category = FoodCategory.Bakery, MealsCount = 3 },
            new RecipientMealDistribution { PickupDocumentId = 1, RecipientId = 2, CharityOrganizationId = 1, Category = FoodCategory.Bakery, MealsCount = 2 });
        await context.SaveChangesAsync();

        var service = new ImpactReportService(new UnitOfWork(context));
        var result = await service.GetOrganizationImpactAsync(1, null, null);

        Assert.NotNull(result);
        Assert.Equal(5, result.RecordedMealsDistributed);
        Assert.Null(result.MealsDistributedEstimate);
        Assert.Equal(2, result.UniqueRecipientsHelped);
    }

    [Fact]
    public async Task Organization_report_uses_meal_estimate_when_no_distribution_records_exist()
    {
        var options = new DbContextOptionsBuilder<Bite4AllContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new Bite4AllContext(options);
        context.CharityOrganizations.Add(new CharityOrganization
        {
            Id = 1,
            Name = "Organizacija",
            Address = "Adresa",
            CityId = 1,
            ContactEmail = "o@x.local",
            ContactPhone = "2",
            TaxIdentificationNumber = "2",
            ApprovalStatus = ApprovalStatus.Approved,
            DailyCapacityKg = 100
        });
        context.FoodOffers.Add(new FoodOffer
        {
            Id = 1,
            HospitalityPartnerId = 1,
            TotalQuantityKg = 10,
            Category = FoodCategory.Bakery,
            PickupWindowStartUtc = DateTime.UtcNow,
            PickupWindowEndUtc = DateTime.UtcNow.AddHours(1),
            ExpiresAtUtc = DateTime.UtcNow.AddHours(2)
        });
        context.PickupDocuments.Add(new PickupDocument
        {
            Id = 1,
            DocumentNumber = "PR-2026-00004",
            FoodOfferId = 1,
            HospitalityPartnerId = 1,
            CharityOrganizationId = 1,
            PlannedQuantityKg = 10,
            ActualQuantityKg = 9,
            Status = PickupStatus.PickedUp
        });
        await context.SaveChangesAsync();

        var service = new ImpactReportService(new UnitOfWork(context));
        var result = await service.GetOrganizationImpactAsync(1, null, null);

        Assert.NotNull(result);
        Assert.Equal(18, result.MealsDistributedEstimate);
        Assert.Null(result.RecordedMealsDistributed);
    }

    /// <summary>
    /// Fix: organization impact date filter must also apply to RecipientMealDistributions.
    /// A distribution recorded outside the requested window must not be counted.
    /// </summary>
    [Fact]
    public async Task Organization_report_date_filter_applies_to_distributions()
    {
        var options = new DbContextOptionsBuilder<Bite4AllContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new Bite4AllContext(options);
        context.CharityOrganizations.Add(new CharityOrganization
        {
            Id = 1,
            Name = "Organizacija",
            Address = "Adresa",
            CityId = 1,
            ContactEmail = "o@x.local",
            ContactPhone = "2",
            TaxIdentificationNumber = "2",
            ApprovalStatus = ApprovalStatus.Approved,
            DailyCapacityKg = 100
        });
        context.FoodOffers.Add(new FoodOffer
        {
            Id = 1,
            HospitalityPartnerId = 1,
            TotalQuantityKg = 10,
            Category = FoodCategory.Bakery,
            PickupWindowStartUtc = DateTime.UtcNow,
            PickupWindowEndUtc = DateTime.UtcNow.AddHours(1),
            ExpiresAtUtc = DateTime.UtcNow.AddHours(2)
        });
        context.PickupDocuments.Add(new PickupDocument
        {
            Id = 1,
            DocumentNumber = "PR-2026-00005",
            FoodOfferId = 1,
            HospitalityPartnerId = 1,
            CharityOrganizationId = 1,
            PlannedQuantityKg = 10,
            ActualQuantityKg = 10,
            Status = PickupStatus.PickedUp
        });
        context.Recipients.AddRange(
            new Recipient { Id = 1, CharityOrganizationId = 1, InternalCode = "R-1" },
            new Recipient { Id = 2, CharityOrganizationId = 1, InternalCode = "R-2" });

        // Old distribution (last year) — should be excluded by date filter
        var oldDistribution = new RecipientMealDistribution
        {
            PickupDocumentId = 1,
            RecipientId = 1,
            CharityOrganizationId = 1,
            Category = FoodCategory.Bakery,
            MealsCount = 100
        };
        oldDistribution.GetType().GetProperty("DistributedAtUtc")!.SetValue(oldDistribution, DateTime.UtcNow.AddYears(-1));

        // Recent distribution — should be included
        var recentDistribution = new RecipientMealDistribution
        {
            PickupDocumentId = 1,
            RecipientId = 2,
            CharityOrganizationId = 1,
            Category = FoodCategory.Bakery,
            MealsCount = 5
        };

        context.RecipientMealDistributions.AddRange(oldDistribution, recentDistribution);
        await context.SaveChangesAsync();

        var service = new ImpactReportService(new UnitOfWork(context));
        // Filter to only the last month — old distribution must be excluded
        var result = await service.GetOrganizationImpactAsync(1, DateTime.UtcNow.AddMonths(-1), null);

        Assert.NotNull(result);
        Assert.Equal(5, result.RecordedMealsDistributed);
        // Only recipient 2 is in-window
        Assert.Equal(1, result.UniqueRecipientsHelped);
    }
}
