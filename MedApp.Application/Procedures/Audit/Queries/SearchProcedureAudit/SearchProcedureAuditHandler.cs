using MedApp.Application.Common.Paging;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Common;
using MedApp.Contracts.Procedures.Audit.Responses;
using MediatR;
using MedApp.Application.Procedures.Audit.Common;

namespace MedApp.Application.Procedures.Audit.Queries.SearchProcedureAudit;

public sealed class SearchProcedureAuditHandler(IProcedureAuditRepository repository)
    : IRequestHandler<SearchProcedureAuditQuery, PagedResponse<PatientProcedureAuditEntryResponse>>
{
    public async Task<PagedResponse<PatientProcedureAuditEntryResponse>> Handle(
        SearchProcedureAuditQuery request,
        CancellationToken cancellationToken)
    {
        var result = await repository.SearchAsync(request, cancellationToken);

        return PagedMapper.ToResponse(result, request.Page, request.PageSize, AuditEntryMapper.ToResponse);
    }
}
