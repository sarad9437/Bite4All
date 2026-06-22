using Bite4All.Application.DTOs.FoodOffers;
using Bite4All.Application.Services;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Infrastructure;
using Bite4All.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bite4All.Tests;

public class FoodOfferServiceSearchTests
{
    [Fact]
    public async Task Search_filters_offers_by_category_and_city()
    {
        var options = new DbContextOptionsBuilder<Bite4AllContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new Bite4AllContext(options);
        context.Cities.AddRange(
            new City { Id = 1, Name = "Beograd" },
            new City { Id = 2, Name = "Novi Sad" });
        context.HospitalityPartners.AddRange(
            new HospitalityPartner
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
            },
            new HospitalityPartner
            {
                Id = 2,
                Name = "Restoran",
                PartnerType = "Restoran",
                Address = "B",
                CityId = 2,
                ContactEmail = "r@x.local",
                ContactPhone = "2",
                TaxIdentificationNumber = "2",
                ApprovalStatus = ApprovalStatus.Approved
            });
        context.FoodOffers.AddRange(
            new FoodOffer
            {
                Id = 1,
                HospitalityPartnerId = 1,
                TotalQuantityKg = 10,
                Category = FoodCategory.Bakery,
                Status = FoodOfferStatus.Active,
                PickupWindowStartUtc = DateTime.UtcNow.AddHours(1),
                PickupWindowEndUtc = DateTime.UtcNow.AddHours(2),
                ExpiresAtUtc = DateTime.UtcNow.AddHours(3)
            },
            new FoodOffer
            {
                Id = 2,
                HospitalityPartnerId = 2,
                TotalQuantityKg = 20,
                Category = FoodCategory.CookedMeal,
                Status = FoodOfferStatus.Active,
                PickupWindowStartUtc = DateTime.UtcNow.AddHours(1),
                PickupWindowEndUtc = DateTime.UtcNow.AddHours(2),
                ExpiresAtUtc = DateTime.UtcNow.AddHours(3)
            });
        await context.SaveChangesAsync();

        var service = new FoodOfferService(new UnitOfWork(context));
        var result = await service.SearchAsync(new FoodOfferSearchRequest
        {
            CityId = 1,
            Category = FoodCategory.Bakery
        });

        Assert.Single(result.Items);
        Assert.Equal("Pekara", result.Items[0].PartnerName);
    }
}
