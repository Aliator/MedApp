using MedApp.Application.Common.Paging;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Common;
using MedApp.Contracts.Procedures.Audit.Responses;
using MediatR;

namespace MedApp.Application.Procedures.Audit.Queries.SearchProcedureAudit;

public sealed class SearchProcedureAuditHandler(IProcedureAuditRepository repository)
    : IRequestHandler<SearchProcedureAuditQuery, PagedResponse<PatientProcedureAuditEntryResponse>>
{
    public async Task<PagedResponse<PatientProcedureAuditEntryResponse>> Handle(
        SearchProcedureAuditQuery request,
        CancellationToken cancellationToken)
    {
        var result = await repository.SearchAsync(request, cancellationToken);

        var items = result.Items
            .Select(e => new PatientProcedureAuditEntryResponse
            {
                Id = e.Id,
                PatientProcedureId = e.PatientProcedureId,
                ProcedureTitle = e.ProcedureTitle,
                PatientId = e.PatientId,
                StageId = e.StageId,
                StageName = e.StageName,
                Action = e.Action.ToString(),
                UserId = e.UserId,
                Username = e.Username,
                AtUtc = e.AtUtc,
                Details = e.Details
            })
            .ToList();

        return new PagedResponse<PatientProcedureAuditEntryResponse>(
            items,
            request.Page,
            request.PageSize,
            result.TotalCount,
            Paging.TotalPages(result.TotalCount, request.PageSize));
    }
}
