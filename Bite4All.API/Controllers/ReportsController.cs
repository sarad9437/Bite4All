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
public class ReportsController(
    IImpactReportService impactReportService,
    IUnitOfWork unitOfWork,
    ISender sender,
    ICsvExportService csvExportService) : ControllerBase
{
    [Authorize(Roles = "Administrator")]
    [HttpGet("admin-impact")]
    public async Task<ActionResult> GetAdminImpact([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, CancellationToken cancellationToken)
    {
        return Ok(await sender.Send(new GetAdminImpactQuery(fromUtc, toUtc), cancellationToken));
    }

    [Authorize(Roles = "Administrator")]
    [HttpGet("admin-impact/export")]
    public async Task<IActionResult> ExportAdminImpact([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, CancellationToken cancellationToken)
    {
        var report = await sender.Send(new GetAdminImpactQuery(fromUtc, toUtc), cancellationToken);
        var bytes = csvExportService.ExportAdminImpact(report);
        return File(bytes, "text/csv", $"admin-impact-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
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

    [Authorize(Roles = "HospitalityPartner,Administrator")]
    [HttpGet("partner-impact/{hospitalityPartnerId:int}/export")]
    public async Task<IActionResult> ExportPartnerImpact(int hospitalityPartnerId, [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, CancellationToken cancellationToken)
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
        if (report is null)
        {
            return NotFound();
        }

        var bytes = csvExportService.ExportPartnerImpact(report);
        return File(bytes, "text/csv", $"partner-{hospitalityPartnerId}-impact-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
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

    [Authorize(Roles = "CharityOrganization,Administrator")]
    [HttpGet("organization-impact/{charityOrganizationId:int}/export")]
    public async Task<IActionResult> ExportOrganizationImpact(int charityOrganizationId, [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, CancellationToken cancellationToken)
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
        if (report is null)
        {
            return NotFound();
        }

        var bytes = csvExportService.ExportOrganizationImpact(report);
        return File(bytes, "text/csv", $"organization-{charityOrganizationId}-impact-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    /// <summary>
    /// Returns impact report for a driver — completed pickups, transported kg,
    /// reputation score history and badge progress.
    /// Accessible by the driver themselves, their organization's admin, or a platform administrator.
    /// </summary>
    [Authorize(Roles = "Driver,CharityOrganization,Administrator")]
    [HttpGet("driver-impact/{driverId:int}")]
    public async Task<ActionResult> GetDriverImpact(int driverId, [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, CancellationToken cancellationToken)
    {
        if (!await CanAccessDriverReportAsync(driverId, cancellationToken))
        {
            return Forbid();
        }

        var report = await impactReportService.GetDriverImpactAsync(driverId, fromUtc, toUtc, cancellationToken);
        return report is null ? NotFound() : Ok(report);
    }

    [Authorize(Roles = "Driver,CharityOrganization,Administrator")]
    [HttpGet("driver-impact/{driverId:int}/export")]
    public async Task<IActionResult> ExportDriverImpact(int driverId, [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, CancellationToken cancellationToken)
    {
        if (!await CanAccessDriverReportAsync(driverId, cancellationToken))
        {
            return Forbid();
        }

        var report = await impactReportService.GetDriverImpactAsync(driverId, fromUtc, toUtc, cancellationToken);
        if (report is null)
        {
            return NotFound();
        }

        var bytes = csvExportService.ExportDriverImpact(report);
        return File(bytes, "text/csv", $"driver-{driverId}-impact-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    private async Task<bool> CanAccessDriverReportAsync(int driverId, CancellationToken cancellationToken)
    {
        // Driver can only see their own report
        if (User.IsAdministrator() || User.DriverId() == driverId)
        {
            return true;
        }

        // A CharityOrganization user may access reports for their own drivers
        if (User.CharityOrganizationId() is not int orgId)
        {
            return false;
        }

        var driver = await unitOfWork.Drivers.GetByIdAsync(driverId, cancellationToken);
        if (driver is null || driver.CharityOrganizationId != orgId)
        {
            return false;
        }

        var organization = await unitOfWork.CharityOrganizations.GetByIdAsync(orgId, cancellationToken);
        return organization is not null && organization.ApprovalStatus == ApprovalStatus.Approved;
    }
}
