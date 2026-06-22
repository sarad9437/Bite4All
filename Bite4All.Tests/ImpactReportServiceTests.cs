using Bite4All.Application.Services;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Infrastructure;
using Bite4All.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bite4All.Tests;

public class ImpactReportServiceTests
{
    [Fact]
    public async Task Admin_impact_uses_actual_pickup_quantity_when_available()
    {
        var options = new DbContextOptionsBuilder<Bite4AllContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new Bite4AllContext(options);
        context.Cities.Add(new City { Id = 1, Name = "Beograd" });
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
        context.PickupDocuments.Add(new PickupDocument
        {
            Id = 1,
            DocumentNumber = "PR-2026-00001",
            FoodOfferId = 1,
            HospitalityPartnerId = 1,
            CharityOrganizationId = 1,
            PlannedQuantityKg = 10,
            ActualQuantityKg = 8,
            Status = PickupStatus.PickedUp
        });
        await context.SaveChangesAsync();

        var service = new ImpactReportService(new UnitOfWork(context));
        var result = await service.GetAdminImpactAsync(null, null);

        Assert.Equal(8, result.TotalRescuedKg);
        Assert.Equal(20, result.EstimatedCo2SavedKg);
    }

    [Fact]
    public async Task Admin_impact_counts_only_expired_offers_without_completed_pickup_as_unused()
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
            Address = "Adresa",
            CityId = 1,
            ContactEmail = "p@x.local",
            ContactPhone = "1",
            TaxIdentificationNumber = "1",
            ApprovalStatus = ApprovalStatus.Approved
        });
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
        context.FoodOffers.AddRange(
            new FoodOffer
            {
                Id = 1,
                HospitalityPartnerId = 1,
                TotalQuantityKg = 4,
                PickupWindowStartUtc = DateTime.UtcNow.AddHours(-4),
                PickupWindowEndUtc = DateTime.UtcNow.AddHours(-3),
                ExpiresAtUtc = DateTime.UtcNow.AddHours(-2),
                Status = FoodOfferStatus.Expired
            },
            new FoodOffer
            {
                Id = 2,
                HospitalityPartnerId = 1,
                TotalQuantityKg = 7,
                PickupWindowStartUtc = DateTime.UtcNow.AddHours(-4),
                PickupWindowEndUtc = DateTime.UtcNow.AddHours(-3),
                ExpiresAtUtc = DateTime.UtcNow.AddHours(-2),
                Status = FoodOfferStatus.Expired
            });
        context.PickupDocuments.Add(new PickupDocument
        {
            Id = 1,
            DocumentNumber = "PR-2026-00003",
            FoodOfferId = 2,
            HospitalityPartnerId = 1,
            CharityOrganizationId = 1,
            PlannedQuantityKg = 7,
            Status = PickupStatus.PickedUp
        });
        await context.SaveChangesAsync();

        var service = new ImpactReportService(new UnitOfWork(context));
        var result = await service.GetAdminImpactAsync(null, null);

        Assert.Equal(1, result.UnusedOffersCount);
        Assert.Equal(4, result.UnusedOffersKg);
    }

    [Fact]
    public async Task Partner_impact_includes_own_unused_expired_offers()
    {
        var options = new DbContextOptionsBuilder<Bite4AllContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new Bite4AllContext(options);
        context.HospitalityPartners.AddRange(
            new HospitalityPartner
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
            },
            new HospitalityPartner
            {
                Id = 2,
                Name = "Restoran",
                PartnerType = "Restoran",
                Address = "Adresa",
                CityId = 1,
                ContactEmail = "r@x.local",
                ContactPhone = "2",
                TaxIdentificationNumber = "3",
                ApprovalStatus = ApprovalStatus.Approved
            });
        context.FoodOffers.AddRange(
            new FoodOffer
            {
                Id = 1,
                HospitalityPartnerId = 1,
                TotalQuantityKg = 6,
                PickupWindowStartUtc = DateTime.UtcNow.AddHours(-4),
                PickupWindowEndUtc = DateTime.UtcNow.AddHours(-3),
                ExpiresAtUtc = DateTime.UtcNow.AddHours(-2),
                Status = FoodOfferStatus.Expired
            },
            new FoodOffer
            {
                Id = 2,
                HospitalityPartnerId = 2,
                TotalQuantityKg = 10,
                PickupWindowStartUtc = DateTime.UtcNow.AddHours(-4),
                PickupWindowEndUtc = DateTime.UtcNow.AddHours(-3),
                ExpiresAtUtc = DateTime.UtcNow.AddHours(-2),
                Status = FoodOfferStatus.Expired
            });
        await context.SaveChangesAsync();

        var service = new ImpactReportService(new UnitOfWork(context));
        var result = await service.GetPartnerImpactAsync(1, null, null);

        Assert.NotNull(result);
        Assert.Equal(1, result.UnusedOffersCount);
        Assert.Equal(6, result.UnusedOffersKg);
    }
}
