using Bite4All.API.Authorization;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bite4All.API.Controllers;

[ApiController]
[Authorize]
[Route("notifications")]
public class NotificationsController(IUnitOfWork unitOfWork) : ControllerBase
{
    [HttpGet]
    public ActionResult<List<Notification>> GetMine([FromQuery] ActorType recipientType, [FromQuery] int recipientId)
    {
        if (!CanAccess(recipientType, recipientId))
        {
            return Forbid();
        }

        return Ok(unitOfWork.Notifications.Query()
            .Where(n => n.RecipientType == recipientType && n.RecipientId == recipientId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToList());
    }

    [HttpPut("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id, CancellationToken cancellationToken)
    {
        var notification = await unitOfWork.Notifications.GetByIdAsync(id, cancellationToken);
        if (notification is null)
        {
            return NotFound();
        }

        if (!CanAccess(notification.RecipientType, notification.RecipientId))
        {
            return Forbid();
        }

        notification.IsRead = true;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private bool CanAccess(ActorType recipientType, int recipientId)
    {
        if (recipientType == ActorType.Administrator && recipientId == 0)
        {
            return User.IsAdministrator();
        }

        return User.IsAdministrator()
            || recipientType == ActorType.HospitalityPartner && User.HospitalityPartnerId() == recipientId
            || recipientType == ActorType.CharityOrganization && User.CharityOrganizationId() == recipientId
            || recipientType == ActorType.Driver && User.DriverId() == recipientId;
    }
}
