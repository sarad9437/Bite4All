using System.Security.Claims;
using Bite4All.API.Controllers;
using Bite4All.API.Hubs;
using Bite4All.Application.DTOs.Common;
using Bite4All.Domain.Enums;
using Bite4All.Domain.Entities;
using Bite4All.Infrastructure;
using Bite4All.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bite4All.Tests;

public class MessagesControllerTests
{
    [Fact]
    public async Task Organization_can_message_partner_in_offer_context_without_pickup()
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
        context.FoodOffers.Add(new FoodOffer
        {
            Id = 1,
            HospitalityPartnerId = 1,
            TotalQuantityKg = 10,
            PickupWindowStartUtc = DateTime.UtcNow.AddHours(1),
            PickupWindowEndUtc = DateTime.UtcNow.AddHours(2),
            ExpiresAtUtc = DateTime.UtcNow.AddHours(3),
            Status = FoodOfferStatus.Active
        });
        context.OfferMatches.Add(new OfferMatch
        {
            FoodOfferId = 1,
            CharityOrganizationId = 1,
            Decision = MatchDecision.Pending,
            Rank = 1,
            Score = 1
        });
        await context.SaveChangesAsync();

        var notifications = new FakeNotificationPublisher();
        var controller = new MessagesController(new UnitOfWork(context), notifications)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("charityOrganizationId", "1")
                    ], "Test"))
                }
            }
        };

        var result = await controller.Send(new CreateMessageRequest
        {
            SenderType = ActorType.CharityOrganization,
            SenderId = 1,
            RecipientType = ActorType.HospitalityPartner,
            RecipientId = 1,
            FoodOfferId = 1,
            Body = "Mozemo da preuzmemo ponudu."
        }, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var message = Assert.IsType<PlatformMessage>(okResult.Value);
        Assert.Equal(ActorType.CharityOrganization, message.SenderType);
        Assert.Equal(ActorType.HospitalityPartner, message.RecipientType);
        Assert.Equal(1, message.FoodOfferId);
        Assert.Null(message.PickupDocumentId);
        Assert.Single(context.PlatformMessages);
        Assert.Equal(1, notifications.SentCount);
    }

    private sealed class FakeNotificationPublisher : INotificationPublisher
    {
        public int SentCount { get; private set; }

        public Task NotifyAsync(
            ActorType actorType,
            int actorId,
            string title,
            string body,
            CancellationToken cancellationToken = default,
            NotificationType type = NotificationType.AdminMessage,
            ActorType? sourceActorType = null,
            int? sourceActorId = null)
        {
            SentCount++;
            return Task.CompletedTask;
        }
    }
}
