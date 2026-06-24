using Bite4All.API.Authorization;
using Bite4All.Application.DTOs.Common;
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
    /// Returns all approved hospitality partners — paginated.
    /// </summary>
    [HttpGet]
    public ActionResult<PagedResult<HospitalityPartner>> GetApproved(
        [FromQuery] int? cityId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = unitOfWork.HospitalityPartners.Query()
            .Where(p => p.ApprovalStatus == ApprovalStatus.Approved);

        if (cityId.HasValue)
        {
            query = query.Where(p => p.CityId == cityId.Value);
        }

        var totalCount = query.Count();
        var items = query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new PagedResult<HospitalityPartner>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
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
    /// Returns all approved charity organizations — paginated.
    /// </summary>
    [Authorize]
    [HttpGet("/organizations")]
    public ActionResult<PagedResult<CharityOrganization>> GetApprovedOrganizations(
        [FromQuery] int? cityId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = unitOfWork.CharityOrganizations.Query()
            .Where(o => o.ApprovalStatus == ApprovalStatus.Approved);

        if (cityId.HasValue)
        {
            query = query.Where(o => o.CityId == cityId.Value);
        }

        var totalCount = query.Count();
        var items = query
            .OrderBy(o => o.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new PagedResult<CharityOrganization>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
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
