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
    [HttpGet]
    public async Task<ActionResult<List<BlockRelation>>> GetAll(CancellationToken cancellationToken)
    {
        if (!User.IsAdministrator())
        {
            return Forbid();
        }

        return Ok(unitOfWork.BlockRelations.Query().ToList());
    }

    [HttpPost]
    public async Task<ActionResult<BlockRelation>> Create(CreateBlockRequest request, CancellationToken cancellationToken)
    {
        // Fix: strict role-based validation — a hospitality partner cannot set BlockedByOrganization
        // and a charity organization cannot set BlockedByHospitalityPartner.
        if (!User.IsAdministrator())
        {
            var isPartner = User.HospitalityPartnerId() == request.HospitalityPartnerId;
            var isOrganization = User.CharityOrganizationId() == request.CharityOrganizationId;

            if (request.BlockedByHospitalityPartner && request.BlockedByOrganization)
            {
                // Only admin can set both flags at once
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

            // A partner must NOT be able to set BlockedByOrganization
            if (isPartner && !isOrganization && request.BlockedByOrganization)
            {
                return Forbid();
            }

            // An organization must NOT be able to set BlockedByHospitalityPartner
            if (isOrganization && !isPartner && request.BlockedByHospitalityPartner)
            {
                return Forbid();
            }

            if (!isPartner && !isOrganization)
            {
                return Forbid();
            }

            // Verify approval status for the acting party
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
