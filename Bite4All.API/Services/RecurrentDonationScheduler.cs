using Bite4All.API.Hubs;
using Bite4All.Application.Services;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Domain.Repositories;

namespace Bite4All.API.Services;

public class RecurrentDonationScheduler(IServiceScopeFactory scopeFactory, ILogger<RecurrentDonationScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRecurrentDonationsAsync(stoppingToken);
                await ActivateUnconfirmedOffersAsync(stoppingToken);
                await ExpireOffersAsync(stoppingToken);
                await ProcessMatchTimeoutsAsync(stoppingToken);
                await ResetExpiredTemporaryCapacitiesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process recurrent donations.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task ProcessRecurrentDonationsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var notificationPublisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();

        var nowLocal = DateTime.Now;
        var currentTime = TimeOnly.FromDateTime(nowLocal);
        var todayUtc = DateTime.UtcNow.Date;

        var dueDonations = unitOfWork.RecurrentDonations.Query()
            .Where(r => r.Status == RecurrentDonationStatus.Active && r.LocalCreationTime <= currentTime)
            .ToList();

        foreach (var recurrent in dueDonations)
        {
            var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(recurrent.HospitalityPartnerId, cancellationToken);
            if (partner is null || partner.ApprovalStatus != ApprovalStatus.Approved)
            {
                continue;
            }

            // Fix: check whether this specific recurrent donation (not any from the partner) already
            // produced an offer today. The old code keyed only on HospitalityPartnerId, which meant
            // that a partner with two separate recurrent schedules would have the second one silently
            // skipped every day after the first one ran.
            var alreadyCreatedToday = unitOfWork.FoodOffers.Query().Any(o =>
                o.CreatedFromRecurrentDonation &&
                o.HospitalityPartnerId == recurrent.HospitalityPartnerId &&
                o.RecurrentDonationId == recurrent.Id &&
                o.CreatedAtUtc.Date == todayUtc);

            if (alreadyCreatedToday)
            {
                continue;
            }

            var offer = new FoodOffer
            {
                HospitalityPartnerId = recurrent.HospitalityPartnerId,
                TotalQuantityKg = recurrent.ExpectedQuantityKg,
                Category = recurrent.Category,
                PickupWindowStartUtc = todayUtc.Add(recurrent.LocalPickupStart.ToTimeSpan()),
                PickupWindowEndUtc = todayUtc.Add(recurrent.LocalPickupEnd.ToTimeSpan()),
                ExpiresAtUtc = todayUtc.Add(recurrent.LocalPickupEnd.ToTimeSpan()).AddHours(Math.Max(recurrent.ShelfLifeHours, 2)),
                Note = recurrent.NoteTemplate,
                Status = FoodOfferStatus.PendingRestaurantConfirmation,
                CreatedFromRecurrentDonation = true,
                RecurrentDonationId = recurrent.Id
            };

            offer.Items.Add(new FoodOfferItem
            {
                FoodOffer = offer,
                Name = "Recurrent donation",
                Quantity = recurrent.ExpectedQuantityKg,
                Unit = "kg"
            });

            await unitOfWork.FoodOffers.AddAsync(offer, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await notificationPublisher.NotifyAsync(
                ActorType.HospitalityPartner,
                recurrent.HospitalityPartnerId,
                "Recurrent offer created",
                "A recurrent Bite4All offer was created for today. You have 30 minutes to cancel it before it becomes active.",
                cancellationToken,
                NotificationType.MatchingOffer);
        }
    }

    private async Task ActivateUnconfirmedOffersAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var matchingService = scope.ServiceProvider.GetRequiredService<IMatchingService>();
        var notificationPublisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();

        var cutoff = DateTime.UtcNow.AddMinutes(-30);
        var pendingOffers = unitOfWork.FoodOffers.Query()
            .Where(o => o.CreatedFromRecurrentDonation &&
                        o.Status == FoodOfferStatus.PendingRestaurantConfirmation &&
                        o.CreatedAtUtc <= cutoff)
            .ToList();

        foreach (var offer in pendingOffers)
        {
            offer.Status = FoodOfferStatus.Active;
        }

        if (pendingOffers.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        foreach (var offer in pendingOffers)
        {
            var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(offer.HospitalityPartnerId, cancellationToken);
            if (partner is null || partner.ApprovalStatus != ApprovalStatus.Approved)
            {
                continue;
            }

            var matches = await matchingService.GenerateMatchesAsync(offer.Id, cancellationToken);
            var first = matches.FirstOrDefault();
            if (first is not null)
            {
                var firstMatch = unitOfWork.OfferMatches.Query()
                    .Where(m => m.FoodOfferId == offer.Id && m.CharityOrganizationId == first.OrganizationId && m.Decision == MatchDecision.Pending)
                    .OrderBy(m => m.Rank)
                    .FirstOrDefault();
                if (firstMatch is not null)
                {
                    firstMatch.NotifiedAtUtc = DateTime.UtcNow;
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }

                await notificationPublisher.NotifyAsync(
                    ActorType.CharityOrganization,
                    first.OrganizationId,
                    "New recurrent food offer",
                    $"A recurrent Bite4All offer #{offer.Id} is available and you are ranked first.",
                    cancellationToken,
                    NotificationType.MatchingOffer,
                    ActorType.HospitalityPartner,
                    offer.HospitalityPartnerId);
            }
            else
            {
                offer.Status = FoodOfferStatus.PublicFallback;
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private async Task ProcessMatchTimeoutsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var notificationPublisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();

        var now = DateTime.UtcNow;
        var candidateMatches = unitOfWork.OfferMatches.Query()
            .Where(m => m.Decision == MatchDecision.Pending && m.NotifiedAtUtc.HasValue)
            .OrderBy(m => m.FoodOfferId)
            .ThenBy(m => m.Rank)
            .ToList()
            .GroupBy(m => m.FoodOfferId)
            .Select(g => g.OrderBy(m => m.Rank).First())
            .ToList();

        var expiredMatches = new List<OfferMatch>();
        foreach (var match in candidateMatches)
        {
            var offer = await unitOfWork.FoodOffers.GetByIdAsync(match.FoodOfferId, cancellationToken);
            if (offer is null)
            {
                continue;
            }

            var windowMinutes = Math.Max(offer.MatchResponseWindowMinutes, 1);
            if (match.NotifiedAtUtc!.Value.AddMinutes(windowMinutes) <= now)
            {
                expiredMatches.Add(match);
            }
        }

        foreach (var match in expiredMatches)
        {
            var offer = await unitOfWork.FoodOffers.GetByIdAsync(match.FoodOfferId, cancellationToken);
            if (offer is null)
            {
                continue;
            }

            match.Decision = MatchDecision.TimedOut;
            match.RespondedAtUtc = DateTime.UtcNow;
            match.DecisionNote = "Response window expired.";

            await notificationPublisher.NotifyAsync(
                ActorType.CharityOrganization,
                match.CharityOrganizationId,
                "Match response window expired",
                $"Your response window for Bite4All offer #{offer.Id} has expired and the offer has been passed to the next organization.",
                cancellationToken,
                NotificationType.MatchingOffer);

            var nextPending = unitOfWork.OfferMatches.Query()
                .Where(m => m.FoodOfferId == offer.Id && m.Decision == MatchDecision.Pending && m.Rank > match.Rank)
                .OrderBy(m => m.Rank)
                .FirstOrDefault();

            if (nextPending is not null)
            {
                nextPending.NotifiedAtUtc = DateTime.UtcNow;
                await notificationPublisher.NotifyAsync(
                    ActorType.CharityOrganization,
                    nextPending.CharityOrganizationId,
                    "Food offer available",
                    $"Bite4All offer #{offer.Id} is now available for your organization.",
                    cancellationToken,
                    NotificationType.MatchingOffer,
                    ActorType.HospitalityPartner,
                    offer.HospitalityPartnerId);
            }
            else if (offer.Status == FoodOfferStatus.Active)
            {
                offer.Status = FoodOfferStatus.PublicFallback;
            }
        }

        if (expiredMatches.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ExpireOffersAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var now = DateTime.UtcNow;
        var expirableStatuses = new[]
        {
            FoodOfferStatus.PendingRestaurantConfirmation,
            FoodOfferStatus.Active,
            FoodOfferStatus.PublicFallback
        };
        var expiredOffers = unitOfWork.FoodOffers.Query()
            .Where(o =>
                o.ExpiresAtUtc <= now &&
                expirableStatuses.Contains(o.Status))
            .ToList();

        foreach (var offer in expiredOffers)
        {
            offer.Status = FoodOfferStatus.Expired;

            var pendingMatches = unitOfWork.OfferMatches.Query()
                .Where(m => m.FoodOfferId == offer.Id && m.Decision == MatchDecision.Pending)
                .ToList();

            foreach (var match in pendingMatches)
            {
                match.Decision = MatchDecision.TimedOut;
                match.RespondedAtUtc = now;
                match.DecisionNote = "Offer expired.";
            }
        }

        if (expiredOffers.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ResetExpiredTemporaryCapacitiesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var now = DateTime.UtcNow;
        var expired = unitOfWork.CharityOrganizations.Query()
            .Where(o => o.TemporaryExtraCapacityKg > 0
                        && o.TemporaryCapacityExpiresAtUtc.HasValue
                        && o.TemporaryCapacityExpiresAtUtc <= now)
            .ToList();

        foreach (var organization in expired)
        {
            organization.TemporaryExtraCapacityKg = 0;
            organization.TemporaryCapacityExpiresAtUtc = null;
        }

        if (expired.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
