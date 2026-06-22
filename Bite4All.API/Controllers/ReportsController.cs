using Bite4All.API.Authorization;
using Bite4All.Application.Queries.Reports;
using Bite4All.Application.Services;
using Bite4All.Domain.Enums;
using Bite4All.Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bite4All.API.Controllers;

[ApiController]
[Authorize]
[Route("reports")]
public class ReportsController(IImpactReportService impactReportService, IUnitOfWork unitOfWork, ISender sender) : ControllerBase
{
    [Authorize(Roles = "Administrator")]
    [HttpGet("admin-impact")]
    public async Task<ActionResult> GetAdminImpact([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, CancellationToken cancellationToken)
    {
        return Ok(await sender.Send(new GetAdminImpactQuery(fromUtc, toUtc), cancellationToken));
    }

    [Authorize(Roles = "HospitalityPartner,Administrator")]
    [HttpGet("partner-impact/{hospitalityPartnerId:int}")]
    public async Task<ActionResult> GetPartnerImpact(int hospitalityPartnerId, [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, CancellationToken cancellationToken)
    {
        if (!User.IsAdministrator() && User.HospitalityPartnerId() != hospitalityPartnerId)
        {
            return Forbid();
        }

        if (!User.IsAdministrator())
        {
            var partner = await unitOfWork.HospitalityPartners.GetByIdAsync(hospitalityPartnerId, cancellationToken);
            if (partner is null || partner.ApprovalStatus != ApprovalStatus.Approved)
            {
                return Forbid();
            }
        }

        var report = await impactReportService.GetPartnerImpactAsync(hospitalityPartnerId, fromUtc, toUtc, cancellationToken);
        return report is null ? NotFound() : Ok(report);
    }

    [Authorize(Roles = "CharityOrganization,Administrator")]
    [HttpGet("organization-impact/{charityOrganizationId:int}")]
    public async Task<ActionResult> GetOrganizationImpact(int charityOrganizationId, [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, CancellationToken cancellationToken)
    {
        if (!User.IsAdministrator() && User.CharityOrganizationId() != charityOrganizationId)
        {
            return Forbid();
        }

        if (!User.IsAdministrator())
        {
            var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(charityOrganizationId, cancellationToken);
            if (organization is null || organization.ApprovalStatus != ApprovalStatus.Approved)
            {
                return Forbid();
            }
        }

        var report = await impactReportService.GetOrganizationImpactAsync(charityOrganizationId, fromUtc, toUtc, cancellationToken);
        return report is null ? NotFound() : Ok(report);
    }
}
