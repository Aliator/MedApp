using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Paging;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Queries.SearchPatientPathways;

/// <summary>
/// A page of pathways. Each shows its patient's name, so the activity log records each patient on the page as seen
/// by the searcher (<see cref="ActivityAction.PatientListed"/>, once per person every few minutes), as a patient search does.
/// </summary>
public sealed class SearchPatientPathwaysHandler(
    IPatientPathwayRepository repository,
    IIdentityReadService identityReadService,
    IActivityLog activityLog,
    MedApp.Application.Teams.Repositories.ITeamRepository teams,
    TimeProvider timeProvider)
    : IRequestHandler<SearchPatientPathwaysQuery, PagedResponse<PatientPathwaySummaryResponse>>
{
    public async Task<PagedResponse<PatientPathwaySummaryResponse>> Handle(
        SearchPatientPathwaysQuery request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        // The lab views (MC7) need the lab's ID.
        if (request.View is not null && request.LabTeamId is null)
            request = request with { LabTeamId = (await teams.GetLabAsync(cancellationToken))?.Id };

        var result = await repository.SearchAsync(request, request.Teams, now, cancellationToken);
        var items = await PathwayMapper.ToSummariesAsync(result.Items, identityReadService, now, cancellationToken);

        await activityLog.RecordViewsAsync(ActivityAction.PatientListed, result.Items.Select(p => p.PatientId), cancellationToken);

        return PagedMapper.ToResponse(new PagedResult<PatientPathwaySummaryResponse>(items, result.TotalCount), request.Page, request.PageSize);
    }
}
