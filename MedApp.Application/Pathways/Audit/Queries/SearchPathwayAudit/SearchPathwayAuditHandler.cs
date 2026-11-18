using MedApp.Application.Common.Paging;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Common;
using MedApp.Contracts.Pathways.Audit.Responses;
using MediatR;
using MedApp.Application.Pathways.Audit.Common;

namespace MedApp.Application.Pathways.Audit.Queries.SearchPathwayAudit;

public sealed class SearchPathwayAuditHandler(IPathwayAuditRepository repository)
    : IRequestHandler<SearchPathwayAuditQuery, PagedResponse<PatientPathwayAuditEntryResponse>>
{
    public async Task<PagedResponse<PatientPathwayAuditEntryResponse>> Handle(
        SearchPathwayAuditQuery request,
        CancellationToken cancellationToken)
    {
        var result = await repository.SearchAsync(request, cancellationToken);

        return PagedMapper.ToResponse(result, request.Page, request.PageSize, AuditEntryMapper.ToResponse);
    }
}
