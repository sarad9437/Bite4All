namespace Bite4All.Application.Services;

/// <summary>
/// Generates CSV exports for impact reports. Keeps formatting logic in one place
/// so every report type (admin/partner/organization/driver) produces a consistent,
/// Excel-friendly CSV (UTF-8 with BOM, CRLF line endings, quoted fields).
/// </summary>
public interface ICsvExportService
{
    byte[] ExportAdminImpact(Bite4All.Application.DTOs.Reports.AdminImpactReportDto report);
    byte[] ExportPartnerImpact(Bite4All.Application.DTOs.Reports.PartnerImpactReportDto report);
    byte[] ExportOrganizationImpact(Bite4All.Application.DTOs.Reports.OrganizationImpactReportDto report);
    byte[] ExportDriverImpact(Bite4All.Application.DTOs.Reports.DriverImpactReportDto report);
}
