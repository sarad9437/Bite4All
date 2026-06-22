using Bite4All.Domain.Enums;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Repositories;
using Microsoft.AspNetCore.SignalR;

namespace Bite4All.API.Hubs;

public class SignalRNotificationPublisher(IHubContext<NotificationsHub> hubContext, IUnitOfWork unitOfWork) : INotificationPublisher
{
    public async Task NotifyAsync(
        ActorType actorType,
        int actorId,
        string title,
        string body,
        CancellationToken cancellationToken = default,
        NotificationType type = NotificationType.AdminMessage,
        ActorType? sourceActorType = null,
        int? sourceActorId = null)
    {
        if (ShouldSuppressNotification(actorType, actorId, sourceActorType, sourceActorId))
        {
            return;
        }

        await unitOfWork.Notifications.AddAsync(new Notification
        {
            RecipientType = actorType,
            RecipientId = actorId,
            Type = type,
            Title = title,
            Body = body
        }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await hubContext.Clients.Group($"{actorType}:{actorId}")
            .SendAsync("notification", new { title, body, createdAtUtc = DateTime.UtcNow }, cancellationToken);
    }

    private bool ShouldSuppressNotification(
        ActorType actorType,
        int actorId,
        ActorType? sourceActorType,
        int? sourceActorId)
    {
        if (!sourceActorId.HasValue)
        {
            return false;
        }

        int hospitalityPartnerId;
        int charityOrganizationId;

        if (actorType == ActorType.CharityOrganization && sourceActorType == ActorType.HospitalityPartner)
        {
            hospitalityPartnerId = sourceActorId.Value;
            charityOrganizationId = actorId;
        }
        else if (actorType == ActorType.HospitalityPartner && sourceActorType == ActorType.CharityOrganization)
        {
            hospitalityPartnerId = actorId;
            charityOrganizationId = sourceActorId.Value;
        }
        else
        {
            return false;
        }

        return unitOfWork.BlockRelations.Query().Any(b =>
            b.IsActive &&
            b.HospitalityPartnerId == hospitalityPartnerId &&
            b.CharityOrganizationId == charityOrganizationId &&
            (b.BlockedByOrganization || b.BlockedByHospitalityPartner));
    }
}
