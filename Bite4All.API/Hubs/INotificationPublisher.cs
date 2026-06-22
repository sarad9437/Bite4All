using Bite4All.Domain.Enums;

namespace Bite4All.API.Hubs;

public interface INotificationPublisher
{
    Task NotifyAsync(
        ActorType actorType,
        int actorId,
        string title,
        string body,
        CancellationToken cancellationToken = default,
        NotificationType type = NotificationType.AdminMessage,
        ActorType? sourceActorType = null,
        int? sourceActorId = null);
}
