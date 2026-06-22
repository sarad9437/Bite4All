using Bite4All.API.Authorization;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bite4All.API.Controllers;

[ApiController]
[Route("partners")]
public class PartnersController(IUnitOfWork unitOfWork) : ControllerBase
{
    /// <summary>
    /// Returns all approved hospitality partners (public — any authenticated or anonymous user
    /// needs to be able to discover partners for context like messaging and matching).
    /// </summary>
    [HttpGet]
    public ActionResult<List<HospitalityPartner>> GetApproved(
        [FromQuery] int? cityId,
        CancellationToken cancellationToken)
    {
        var query = unitOfWork.HospitalityPartners.Query()
            .Where(p => p.ApprovalStatus == ApprovalStatus.Approved);

        if (cityId.HasValue)
        {
            query = query.Where(p => p.CityId == cityId.Value);
        }

        return Ok(query.OrderBy(p => p.Name).ToList());
    }

    /// <summary>
    /// Returns a single approved hospitality partner by id.
    /// Admins can also retrieve partners that are not yet approved.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<HospitalityPartner>> GetById(int id, CancellationToken cancellationToken)
    {
        var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(id, cancellationToken);
        if (partner is null)
        {
            return NotFound();
        }

        var isAdmin = User.Identity?.IsAuthenticated == true && User.IsAdministrator();
        var isOwner = User.HospitalityPartnerId() == id;

        if (partner.ApprovalStatus != ApprovalStatus.Approved && !isAdmin && !isOwner)
        {
            return NotFound();
        }

        return Ok(partner);
    }

    /// <summary>
    /// Returns all approved charity organizations (useful for admin dashboard and partner context).
    /// </summary>
    [Authorize]
    [HttpGet("/organizations")]
    public ActionResult<List<CharityOrganization>> GetApprovedOrganizations(
        [FromQuery] int? cityId,
        CancellationToken cancellationToken)
    {
        var query = unitOfWork.CharityOrganizations.Query()
            .Where(o => o.ApprovalStatus == ApprovalStatus.Approved);

        if (cityId.HasValue)
        {
            query = query.Where(o => o.CityId == cityId.Value);
        }

        return Ok(query.OrderBy(o => o.Name).ToList());
    }

    /// <summary>
    /// Returns a single approved charity organization by id.
    /// </summary>
    [Authorize]
    [HttpGet("/organizations/{id:int}")]
    public async Task<ActionResult<CharityOrganization>> GetOrganizationById(int id, CancellationToken cancellationToken)
    {
        var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(id, cancellationToken);
        if (organization is null)
        {
            return NotFound();
        }

        var isAdmin = User.IsAdministrator();
        var isOwner = User.CharityOrganizationId() == id;

        if (organization.ApprovalStatus != ApprovalStatus.Approved && !isAdmin && !isOwner)
        {
            return NotFound();
        }

        return Ok(organization);
    }
}
