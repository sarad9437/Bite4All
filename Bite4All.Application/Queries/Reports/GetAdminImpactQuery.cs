using Bite4All.Application.DTOs.Reports;
using Bite4All.Application.Services;
using MediatR;

namespace Bite4All.Application.Queries.Reports;

public record GetAdminImpactQuery(DateTime? FromUtc, DateTime? ToUtc) : IRequest<AdminImpactReportDto>;

public class GetAdminImpactQueryHandler(IImpactReportService impactReportService)
    : IRequestHandler<GetAdminImpactQuery, AdminImpactReportDto>
{
    public Task<AdminImpactReportDto> Handle(GetAdminImpactQuery request, CancellationToken cancellationToken)
    {
        return impactReportService.GetAdminImpactAsync(request.FromUtc, request.ToUtc, cancellationToken);
    }
}
