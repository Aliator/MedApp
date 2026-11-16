using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Paging;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Common;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Queries.SearchPatientProcedures;

/// <summary>
/// A page of procedures. Each shows its patient's name, so the activity log records each patient on the page as seen
/// by the searcher (<see cref="ActivityAction.PatientListed"/>, once per person every few minutes), as a patient search does.
/// </summary>
public sealed class SearchPatientProceduresHandler(
    IPatientProcedureRepository repository,
    IIdentityReadService identityReadService,
    IActivityLog activityLog,
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

        await activityLog.RecordViewsAsync(ActivityAction.PatientListed, result.Items.Select(p => p.PatientId), cancellationToken);

        return PagedMapper.ToResponse(new PagedResult<PatientProcedureSummaryResponse>(items, result.TotalCount), request.Page, request.PageSize);
    }
}
