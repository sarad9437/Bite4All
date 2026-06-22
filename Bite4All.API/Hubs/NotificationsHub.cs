using Bite4All.API.Authorization;
using Bite4All.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Bite4All.API.Hubs;

[Authorize]
public class NotificationsHub : Hub
{
    public Task JoinActorGroup(string actorType, int actorId)
    {
        if (!Enum.TryParse<ActorType>(actorType, out var parsedActorType) || !CanJoin(parsedActorType, actorId))
        {
            throw new HubException("You cannot join this notification group.");
        }

        return Groups.AddToGroupAsync(Context.ConnectionId, $"{actorType}:{actorId}");
    }

    private bool CanJoin(ActorType actorType, int actorId)
    {
        var user = Context.User;
        if (user is null)
        {
            return false;
        }

        if (actorType == ActorType.Administrator && actorId == 0)
        {
            return user.IsAdministrator();
        }

        return user.IsAdministrator()
            || actorType == ActorType.HospitalityPartner && user.HospitalityPartnerId() == actorId
            || actorType == ActorType.CharityOrganization && user.CharityOrganizationId() == actorId
            || actorType == ActorType.Driver && user.DriverId() == actorId;
    }
}
