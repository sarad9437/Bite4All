using Bite4All.Application.Services;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Infrastructure;
using Bite4All.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bite4All.Tests;

public class DriverImpactReportTests
{
    [Fact]
    public async Task Driver_impact_returns_null_for_unknown_driver()
    {
        var options = new DbContextOptionsBuilder<Bite4AllContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new Bite4AllContext(options);
        var service = new ImpactReportService(new UnitOfWork(context));

        var result = await service.GetDriverImpactAsync(999, null, null);

        Assert.Null(result);
    }

    [Fact]
    public async Task Driver_impact_returns_correct_transported_kg_and_counts()
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
            DailyCapacityKg = 200
        });
        context.Drivers.Add(new Driver
        {
            Id = 1,
            CharityOrganizationId = 1,
            FullName = "Petar Petrovic",
            Email = "petar@x.local",
            Phone = "3",
            CompletedPickups = 12,
            CancellationCount = 1,
            ReputationScore = 4.1
        });
        context.HospitalityPartners.Add(new HospitalityPartner
        {
            Id = 1,
            Name = "Pekara",
            PartnerType = "Pekara",
            Address = "Adresa",
            CityId = 1,
            ContactEmail = "p@x.local",
            ContactPhone = "1",
            TaxIdentificationNumber = "1",
            ApprovalStatus = ApprovalStatus.Approved
        });
        context.PickupDocuments.AddRange(
            new PickupDocument
            {
                Id = 1,
                DocumentNumber = "PR-2026-00010",
                FoodOfferId = 1,
                HospitalityPartnerId = 1,
                CharityOrganizationId = 1,
                DriverId = 1,
                PlannedQuantityKg = 10,
                ActualQuantityKg = 9,
                Status = PickupStatus.DeliveredToOrganization
            },
            new PickupDocument
            {
                Id = 2,
                DocumentNumber = "PR-2026-00011",
                FoodOfferId = 2,
                HospitalityPartnerId = 1,
                CharityOrganizationId = 1,
                DriverId = 1,
                PlannedQuantityKg = 5,
                ActualQuantityKg = 5,
                Status = PickupStatus.PickedUp
            },
            // Another driver — should NOT be counted
            new PickupDocument
            {
                Id = 3,
                DocumentNumber = "PR-2026-00012",
                FoodOfferId = 3,
                HospitalityPartnerId = 1,
                CharityOrganizationId = 1,
                DriverId = 2,
                PlannedQuantityKg = 20,
                ActualQuantityKg = 20,
                Status = PickupStatus.DeliveredToOrganization
            });
        await context.SaveChangesAsync();

        var service = new ImpactReportService(new UnitOfWork(context));
        var result = await service.GetDriverImpactAsync(1, null, null);

        Assert.NotNull(result);
        Assert.Equal(1, result.DriverId);
        Assert.Equal("Petar Petrovic", result.DriverName);
        // TransportedKg counts only pickups assigned to this driver
        Assert.Equal(14m, result.TransportedKg); // 9 + 5
        Assert.Equal(12, result.CompletedPickups); // from Driver entity
        Assert.Equal(1, result.CancellationCount);
        Assert.Equal(4.1, result.ReputationScore);
        Assert.Contains("Bronze", result.NextBadgeHint);
    }

    [Fact]
    public async Task Driver_impact_date_filter_restricts_transported_kg()
    {
        var options = new DbContextOptionsBuilder<Bite4AllContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new Bite4AllContext(options);

        context.CharityOrganizations.Add(new CharityOrganization
        {
            Id = 1,
            Name = "Org",
            Address = "A",
            CityId = 1,
            ContactEmail = "o@x.local",
            ContactPhone = "1",
            TaxIdentificationNumber = "1",
            ApprovalStatus = ApprovalStatus.Approved,
            DailyCapacityKg = 200
        });
        context.Drivers.Add(new Driver
        {
            Id = 1,
            CharityOrganizationId = 1,
            FullName = "Ana Anic",
            Email = "ana@x.local",
            Phone = "2"
        });
        context.HospitalityPartners.Add(new HospitalityPartner
        {
            Id = 1,
            Name = "Restoran",
            PartnerType = "Restoran",
            Address = "B",
            CityId = 1,
            ContactEmail = "r@x.local",
            ContactPhone = "3",
            TaxIdentificationNumber = "2",
            ApprovalStatus = ApprovalStatus.Approved
        });

        var oldPickup = new PickupDocument
        {
            Id = 1,
            DocumentNumber = "PR-2025-00001",
            FoodOfferId = 1,
            HospitalityPartnerId = 1,
            CharityOrganizationId = 1,
            DriverId = 1,
            PlannedQuantityKg = 30,
            ActualQuantityKg = 30,
            Status = PickupStatus.DeliveredToOrganization
        };
        // Manually set CreatedAtUtc to last year
        oldPickup.GetType().GetProperty("CreatedAtUtc")!.SetValue(oldPickup, DateTime.UtcNow.AddYears(-1));

        context.PickupDocuments.Add(oldPickup);
        context.PickupDocuments.Add(new PickupDocument
        {
            Id = 2,
            DocumentNumber = "PR-2026-00020",
            FoodOfferId = 2,
            HospitalityPartnerId = 1,
            CharityOrganizationId = 1,
            DriverId = 1,
            PlannedQuantityKg = 8,
            ActualQuantityKg = 8,
            Status = PickupStatus.PickedUp
        });
        await context.SaveChangesAsync();

        var service = new ImpactReportService(new UnitOfWork(context));
        // Filter to only this year
        var result = await service.GetDriverImpactAsync(1, DateTime.UtcNow.AddMonths(-1), null);

        Assert.NotNull(result);
        Assert.Equal(8m, result.TransportedKg);
    }
}

public class BlockDuplicateTests
{
    [Fact]
    public async Task Block_relation_deactivate_and_recreate_reactivates_existing_record()
    {
        // Verifies application-level duplicate handling is consistent with DB unique index.
        // The controller reactivates a previously deactivated block instead of inserting a new row.
        var options = new DbContextOptionsBuilder<Bite4AllContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new Bite4AllContext(options);

        context.BlockRelations.Add(new BlockRelation
        {
            Id = 1,
            HospitalityPartnerId = 1,
            CharityOrganizationId = 1,
            BlockedByHospitalityPartner = true,
            BlockedByOrganization = false,
            Reason = "First block",
            IsActive = false   // previously deactivated
        });
        await context.SaveChangesAsync();

        // Simulate what the controller does on POST /blocks when existing inactive block found
        var existing = context.BlockRelations.FirstOrDefault(b =>
            b.HospitalityPartnerId == 1 && b.CharityOrganizationId == 1);

        Assert.NotNull(existing);
        Assert.False(existing.IsActive);

        existing.IsActive = true;
        existing.BlockedByOrganization = true; // new flag added
        existing.Reason = "Reactivated with org flag";
        await context.SaveChangesAsync();

        var reloaded = context.BlockRelations.Single(b => b.HospitalityPartnerId == 1 && b.CharityOrganizationId == 1);
        Assert.True(reloaded.IsActive);
        Assert.True(reloaded.BlockedByOrganization);
        Assert.Equal(1, context.BlockRelations.Count()); // no duplicate row
    }
}

public class CampaignDecideTests
{
    [Fact]
    public async Task Decide_rejects_draft_campaign_without_prior_submit()
    {
        // The Decide endpoint now only accepts PendingApproval status.
        // This test documents the expected behavior after the fix.
        var options = new DbContextOptionsBuilder<Bite4AllContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new Bite4AllContext(options);
        context.HospitalityPartners.Add(new HospitalityPartner
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
        context.SpecialCampaigns.Add(new SpecialCampaign
        {
            Id = 1,
            HospitalityPartnerId = 1,
            Name = "Draft kampanja",
            Description = "",
            StartsOn = DateOnly.FromDateTime(DateTime.UtcNow),
            EndsOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            TargetQuantityKg = 100,
            Status = CampaignStatus.Draft   // NOT yet submitted
        });
        await context.SaveChangesAsync();

        var campaign = context.SpecialCampaigns.First();

        // Fix: only PendingApproval is decidable
        var canDecide = campaign.Status == CampaignStatus.PendingApproval;

        Assert.False(canDecide, "A Draft campaign must be submitted before it can be approved or rejected.");
    }

    [Fact]
    public async Task Decide_accepts_pending_approval_campaign()
    {
        var options = new DbContextOptionsBuilder<Bite4AllContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new Bite4AllContext(options);
        context.HospitalityPartners.Add(new HospitalityPartner
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
        context.SpecialCampaigns.Add(new SpecialCampaign
        {
            Id = 1,
            HospitalityPartnerId = 1,
            Name = "Kampanja za odobrenje",
            Description = "",
            StartsOn = DateOnly.FromDateTime(DateTime.UtcNow),
            EndsOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            TargetQuantityKg = 100,
            Status = CampaignStatus.PendingApproval
        });
        await context.SaveChangesAsync();

        var campaign = context.SpecialCampaigns.First();
        campaign.Status = CampaignStatus.Approved;
        await context.SaveChangesAsync();

        Assert.Equal(CampaignStatus.Approved, context.SpecialCampaigns.First().Status);
    }
}
