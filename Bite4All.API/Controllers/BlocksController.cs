using Bite4All.API.Authorization;
using Bite4All.Application.DTOs.Common;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bite4All.API.Controllers;

[ApiController]
[Authorize]
[Route("blocks")]
public class BlocksController(IUnitOfWork unitOfWork) : ControllerBase
{
    /// <summary>
    /// Admin: vraća sve blokade u sistemu — paginirano.
    /// </summary>
    [HttpGet]
    public ActionResult<PagedResult<BlockRelation>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!User.IsAdministrator())
        {
            return Forbid();
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = unitOfWork.BlockRelations.Query();
        var totalCount = query.Count();
        var items = query
            .OrderByDescending(b => b.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new PagedResult<BlockRelation>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    /// <summary>
    /// Vraća blokade u kojima učestvuje trenutno autentifikovani aktor.
    /// Fix: ovaj endpoint je ranije vraćao kompletnu listu bez paginacije —
    /// problematično za partnere/organizacije sa puno akumuliranih blokada.
    /// </summary>
    [HttpGet("my")]
    public ActionResult<PagedResult<BlockRelation>> GetMine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var partnerId = User.HospitalityPartnerId();
        var orgId = User.CharityOrganizationId();

        if (!User.IsAdministrator() && partnerId is null && orgId is null)
        {
            return Forbid();
        }

        IQueryable<BlockRelation> query = unitOfWork.BlockRelations.Query();

        if (partnerId.HasValue && orgId.HasValue)
        {
            query = query.Where(b =>
                b.HospitalityPartnerId == partnerId.Value ||
                b.CharityOrganizationId == orgId.Value);
        }
        else if (partnerId.HasValue)
        {
            query = query.Where(b => b.HospitalityPartnerId == partnerId.Value);
        }
        else if (orgId.HasValue)
        {
            query = query.Where(b => b.CharityOrganizationId == orgId.Value);
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var totalCount = query.Count();
        var items = query
            .OrderByDescending(b => b.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new PagedResult<BlockRelation>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    [HttpPost]
    public async Task<ActionResult<BlockRelation>> Create(Bite4All.Application.DTOs.Common.CreateBlockRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsAdministrator())
        {
            var isPartner = User.HospitalityPartnerId() == request.HospitalityPartnerId;
            var isOrganization = User.CharityOrganizationId() == request.CharityOrganizationId;

            if (request.BlockedByHospitalityPartner && request.BlockedByOrganization)
            {
                return Forbid();
            }

            if (request.BlockedByHospitalityPartner && !isPartner)
            {
                return Forbid();
            }

            if (request.BlockedByOrganization && !isOrganization)
            {
                return Forbid();
            }

            if (isPartner && !isOrganization && request.BlockedByOrganization)
            {
                return Forbid();
            }

            if (isOrganization && !isPartner && request.BlockedByHospitalityPartner)
            {
                return Forbid();
            }

            if (!isPartner && !isOrganization)
            {
                return Forbid();
            }

            if (request.BlockedByHospitalityPartner)
            {
                var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(request.HospitalityPartnerId, cancellationToken);
                if (partner is null || partner.ApprovalStatus != Bite4All.Domain.Enums.ApprovalStatus.Approved)
                {
                    return Forbid();
                }
            }

            if (request.BlockedByOrganization)
            {
                var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(request.CharityOrganizationId, cancellationToken);
                if (organization is null || organization.ApprovalStatus != Bite4All.Domain.Enums.ApprovalStatus.Approved)
                {
                    return Forbid();
                }
            }
        }

        var existing = unitOfWork.BlockRelations.Query().FirstOrDefault(b =>
            b.HospitalityPartnerId == request.HospitalityPartnerId &&
            b.CharityOrganizationId == request.CharityOrganizationId);

        if (existing is not null)
        {
            if (existing.IsActive)
            {
                return Conflict(new { message = "An active block already exists between these two parties." });
            }

            existing.IsActive = true;
            existing.BlockedByHospitalityPartner = request.BlockedByHospitalityPartner || existing.BlockedByHospitalityPartner;
            existing.BlockedByOrganization = request.BlockedByOrganization || existing.BlockedByOrganization;
            existing.Reason = request.Reason;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Ok(existing);
        }

        var block = new BlockRelation
        {
            HospitalityPartnerId = request.HospitalityPartnerId,
            CharityOrganizationId = request.CharityOrganizationId,
            BlockedByHospitalityPartner = request.BlockedByHospitalityPartner,
            BlockedByOrganization = request.BlockedByOrganization,
            Reason = request.Reason
        };

        await unitOfWork.BlockRelations.AddAsync(block, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(block);
    }

    [Authorize(Roles = "Administrator")]
    [HttpPut("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var block = await unitOfWork.BlockRelations.GetByIdAsync(id, cancellationToken);
        if (block is null)
        {
            return NotFound();
        }

        block.IsActive = false;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
