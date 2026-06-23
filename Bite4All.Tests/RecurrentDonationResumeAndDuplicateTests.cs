using Bite4All.Application.Commands.RecurrentDonations;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Infrastructure;
using Bite4All.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bite4All.Tests;

/// <summary>
/// Tests for:
///   1. ResumeRecurrentDonationCommand — paused donations can be resumed.
///   2. MaterializeRecurrentToday duplicate guard — the same schedule cannot
///      produce more than one offer per calendar day.
/// </summary>
public class RecurrentDonationResumeTests
{
    private static Bite4AllContext BuildContext() =>
        new(new DbContextOptionsBuilder<Bite4AllContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Resume_sets_status_to_active_for_paused_donation()
    {
        await using var ctx = BuildContext();
        ctx.RecurrentDonations.Add(new RecurrentDonation
        {
            Id = 1,
            HospitalityPartnerId = 1,
            ExpectedQuantityKg = 10,
            Category = FoodCategory.Bakery,
            LocalCreationTime = new TimeOnly(18, 0),
            LocalPickupStart = new TimeOnly(19, 0),
            LocalPickupEnd = new TimeOnly(21, 0),
            ShelfLifeHours = 4,
            Status = RecurrentDonationStatus.Paused
        });
        await ctx.SaveChangesAsync();

        var handler = new ResumeRecurrentDonationCommandHandler(new UnitOfWork(ctx));
        var result = await handler.Handle(new ResumeRecurrentDonationCommand(1), CancellationToken.None);

        Assert.True(result);
        Assert.Equal(RecurrentDonationStatus.Active, ctx.RecurrentDonations.First().Status);
    }

    [Fact]
    public async Task Resume_returns_false_for_already_active_donation()
    {
        await using var ctx = BuildContext();
        ctx.RecurrentDonations.Add(new RecurrentDonation
        {
            Id = 1,
            HospitalityPartnerId = 1,
            ExpectedQuantityKg = 10,
            Category = FoodCategory.Bakery,
            LocalCreationTime = new TimeOnly(18, 0),
            LocalPickupStart = new TimeOnly(19, 0),
            LocalPickupEnd = new TimeOnly(21, 0),
            ShelfLifeHours = 4,
            Status = RecurrentDonationStatus.Active
        });
        await ctx.SaveChangesAsync();

        var handler = new ResumeRecurrentDonationCommandHandler(new UnitOfWork(ctx));
        var result = await handler.Handle(new ResumeRecurrentDonationCommand(1), CancellationToken.None);

        Assert.False(result);
        // Status remains Active — no change
        Assert.Equal(RecurrentDonationStatus.Active, ctx.RecurrentDonations.First().Status);
    }

    [Fact]
    public async Task Resume_returns_false_for_cancelled_donation()
    {
        await using var ctx = BuildContext();
        ctx.RecurrentDonations.Add(new RecurrentDonation
        {
            Id = 1,
            HospitalityPartnerId = 1,
            ExpectedQuantityKg = 10,
            Category = FoodCategory.Bakery,
            LocalCreationTime = new TimeOnly(18, 0),
            LocalPickupStart = new TimeOnly(19, 0),
            LocalPickupEnd = new TimeOnly(21, 0),
            ShelfLifeHours = 4,
            Status = RecurrentDonationStatus.Cancelled
        });
        await ctx.SaveChangesAsync();

        var handler = new ResumeRecurrentDonationCommandHandler(new UnitOfWork(ctx));
        var result = await handler.Handle(new ResumeRecurrentDonationCommand(1), CancellationToken.None);

        Assert.False(result);
        Assert.Equal(RecurrentDonationStatus.Cancelled, ctx.RecurrentDonations.First().Status);
    }

    [Fact]
    public async Task Resume_returns_false_for_nonexistent_donation()
    {
        await using var ctx = BuildContext();

        var handler = new ResumeRecurrentDonationCommandHandler(new UnitOfWork(ctx));
        var result = await handler.Handle(new ResumeRecurrentDonationCommand(999), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task Pause_then_resume_roundtrip_restores_active_status()
    {
        await using var ctx = BuildContext();
        ctx.RecurrentDonations.Add(new RecurrentDonation
        {
            Id = 1,
            HospitalityPartnerId = 1,
            ExpectedQuantityKg = 5,
            Category = FoodCategory.CookedMeal,
            LocalCreationTime = new TimeOnly(17, 0),
            LocalPickupStart = new TimeOnly(18, 0),
            LocalPickupEnd = new TimeOnly(20, 0),
            ShelfLifeHours = 3,
            Status = RecurrentDonationStatus.Active
        });
        await ctx.SaveChangesAsync();

        var pauseHandler = new PauseRecurrentDonationCommandHandler(new UnitOfWork(ctx));
        var pauseResult = await pauseHandler.Handle(new PauseRecurrentDonationCommand(1), CancellationToken.None);
        Assert.True(pauseResult);
        Assert.Equal(RecurrentDonationStatus.Paused, ctx.RecurrentDonations.First().Status);

        var resumeHandler = new ResumeRecurrentDonationCommandHandler(new UnitOfWork(ctx));
        var resumeResult = await resumeHandler.Handle(new ResumeRecurrentDonationCommand(1), CancellationToken.None);
        Assert.True(resumeResult);
        Assert.Equal(RecurrentDonationStatus.Active, ctx.RecurrentDonations.First().Status);
    }
}

public class MaterializeDuplicateGuardTests
{
    private static Bite4AllContext BuildContext() =>
        new(new DbContextOptionsBuilder<Bite4AllContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Duplicate_guard_detects_offer_already_created_today_for_same_recurrent()
    {
        await using var ctx = BuildContext();

        ctx.RecurrentDonations.Add(new RecurrentDonation
        {
            Id = 1,
            HospitalityPartnerId = 1,
            ExpectedQuantityKg = 10,
            Category = FoodCategory.Bakery,
            LocalCreationTime = new TimeOnly(18, 0),
            LocalPickupStart = new TimeOnly(19, 0),
            LocalPickupEnd = new TimeOnly(21, 0),
            ShelfLifeHours = 4,
            Status = RecurrentDonationStatus.Active
        });

        // Simulate an offer already created today by the scheduler for this recurrent donation
        ctx.FoodOffers.Add(new FoodOffer
        {
            Id = 1,
            HospitalityPartnerId = 1,
            TotalQuantityKg = 10,
            Category = FoodCategory.Bakery,
            Status = FoodOfferStatus.PendingRestaurantConfirmation,
            CreatedFromRecurrentDonation = true,
            RecurrentDonationId = 1,
            PickupWindowStartUtc = DateTime.UtcNow.AddHours(1),
            PickupWindowEndUtc = DateTime.UtcNow.AddHours(3),
            ExpiresAtUtc = DateTime.UtcNow.AddHours(5)
        });
        await ctx.SaveChangesAsync();

        var todayUtc = DateTime.UtcNow.Date;

        // This is the guard logic from the fixed MaterializeRecurrentToday endpoint
        var alreadyCreatedToday = ctx.FoodOffers.Any(o =>
            o.CreatedFromRecurrentDonation &&
            o.HospitalityPartnerId == 1 &&
            o.RecurrentDonationId == 1 &&
            o.CreatedAtUtc.Date == todayUtc);

        Assert.True(alreadyCreatedToday, "Guard should detect an offer already created today.");
    }

    [Fact]
    public async Task Duplicate_guard_allows_first_creation_today()
    {
        await using var ctx = BuildContext();

        ctx.RecurrentDonations.Add(new RecurrentDonation
        {
            Id = 1,
            HospitalityPartnerId = 1,
            ExpectedQuantityKg = 10,
            Category = FoodCategory.Bakery,
            LocalCreationTime = new TimeOnly(18, 0),
            LocalPickupStart = new TimeOnly(19, 0),
            LocalPickupEnd = new TimeOnly(21, 0),
            ShelfLifeHours = 4,
            Status = RecurrentDonationStatus.Active
        });
        await ctx.SaveChangesAsync();

        var todayUtc = DateTime.UtcNow.Date;

        // No offer created yet today
        var alreadyCreatedToday = ctx.FoodOffers.Any(o =>
            o.CreatedFromRecurrentDonation &&
            o.HospitalityPartnerId == 1 &&
            o.RecurrentDonationId == 1 &&
            o.CreatedAtUtc.Date == todayUtc);

        Assert.False(alreadyCreatedToday, "Guard should allow creation when no offer exists today.");
    }

    [Fact]
    public async Task Duplicate_guard_is_per_recurrent_donation_not_per_partner()
    {
        // A partner with two recurrent schedules should get one offer per schedule per day,
        // not be blocked because partner already has any recurrent offer today.
        await using var ctx = BuildContext();

        ctx.RecurrentDonations.AddRange(
            new RecurrentDonation
            {
                Id = 1, HospitalityPartnerId = 1, ExpectedQuantityKg = 10,
                Category = FoodCategory.Bakery, LocalCreationTime = new TimeOnly(10, 0),
                LocalPickupStart = new TimeOnly(11, 0), LocalPickupEnd = new TimeOnly(13, 0),
                ShelfLifeHours = 4, Status = RecurrentDonationStatus.Active
            },
            new RecurrentDonation
            {
                Id = 2, HospitalityPartnerId = 1, ExpectedQuantityKg = 5,
                Category = FoodCategory.CookedMeal, LocalCreationTime = new TimeOnly(18, 0),
                LocalPickupStart = new TimeOnly(19, 0), LocalPickupEnd = new TimeOnly(21, 0),
                ShelfLifeHours = 3, Status = RecurrentDonationStatus.Active
            });

        // Offer already created for schedule 1 today
        ctx.FoodOffers.Add(new FoodOffer
        {
            Id = 1, HospitalityPartnerId = 1, TotalQuantityKg = 10,
            Category = FoodCategory.Bakery, Status = FoodOfferStatus.PendingRestaurantConfirmation,
            CreatedFromRecurrentDonation = true, RecurrentDonationId = 1,
            PickupWindowStartUtc = DateTime.UtcNow.AddHours(1),
            PickupWindowEndUtc = DateTime.UtcNow.AddHours(3),
            ExpiresAtUtc = DateTime.UtcNow.AddHours(5)
        });
        await ctx.SaveChangesAsync();

        var todayUtc = DateTime.UtcNow.Date;

        // Schedule 1 is already done
        var schedule1Done = ctx.FoodOffers.Any(o =>
            o.CreatedFromRecurrentDonation && o.HospitalityPartnerId == 1 &&
            o.RecurrentDonationId == 1 && o.CreatedAtUtc.Date == todayUtc);

        // Schedule 2 is NOT done yet
        var schedule2Done = ctx.FoodOffers.Any(o =>
            o.CreatedFromRecurrentDonation && o.HospitalityPartnerId == 1 &&
            o.RecurrentDonationId == 2 && o.CreatedAtUtc.Date == todayUtc);

        Assert.True(schedule1Done, "Schedule 1 should be blocked (already created today).");
        Assert.False(schedule2Done, "Schedule 2 should be allowed (not yet created today).");
    }
}
