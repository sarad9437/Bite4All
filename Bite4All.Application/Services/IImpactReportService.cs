using Bite4All.Application.DTOs.Reports;

namespace Bite4All.Application.Services;

public interface IImpactReportService
{
    Task<AdminImpactReportDto> GetAdminImpactAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default);
    Task<PartnerImpactReportDto?> GetPartnerImpactAsync(int hospitalityPartnerId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default);
    Task<OrganizationImpactReportDto?> GetOrganizationImpactAsync(int charityOrganizationId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default);
    Task<DriverImpactReportDto?> GetDriverImpactAsync(int driverId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default);
}
