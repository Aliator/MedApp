using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Paging;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Common;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Queries.SearchPatientProcedures;

public sealed class SearchPatientProceduresHandler(
    IPatientProcedureRepository repository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<SearchPatientProceduresQuery, PagedResponse<PatientProcedureSummaryResponse>>
{
    public async Task<PagedResponse<PatientProcedureSummaryResponse>> Handle(
        SearchPatientProceduresQuery request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var result = await repository.SearchAsync(request, now, cancellationToken);
        var items = await ProcedureMapper.ToSummariesAsync(result.Items, identityReadService, now, cancellationToken);

        return new PagedResponse<PatientProcedureSummaryResponse>(
            items,
            request.Page,
            request.PageSize,
            result.TotalCount,
            Paging.TotalPages(result.TotalCount, request.PageSize));
    }
}
