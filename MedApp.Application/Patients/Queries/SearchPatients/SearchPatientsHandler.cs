using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Paging;
using MedApp.Application.Common.Teams;
using MedApp.Application.Patients.Repositories;
using MedApp.Contracts.Common;
using MedApp.Contracts.Patients.Responses;
using MediatR;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Activity;

namespace MedApp.Application.Patients.Queries.SearchPatients;

/// <summary>
/// A page of patients, narrowed to the clinic picker's team if one is picked (400 for a team that isn't the viewer's,
/// unless they're an admin). The activity log records each patient on the page as seen by the searcher (once per person
/// every few minutes; an admin's with <c>access</c> and <c>teamFilter</c>, decision 207), and a search that used filters
/// as which filters and how many matched, never the terms typed, which could themselves be a patient's details. The
/// picker alone is a view, not a search.
/// </summary>
public sealed class SearchPatientsHandler(IPatientRepository repository, IActivityLog activityLog)
    : IRequestHandler<SearchPatientsQuery, PagedResponse<PatientResponse>>
{
    public async Task<PagedResponse<PatientResponse>> Handle(SearchPatientsQuery request, CancellationToken cancellationToken)
    {
        TeamPicker.EnsureCanPick(request.TeamId, request.Teams);
        var result = await repository.SearchAsync(request, cancellationToken);

        var filters = PatientSearch.FiltersUsed(request);
        if (filters.Count > 0)
            await activityLog.RecordAsync(
                activityLog.Entry(ActivityAction.PatientsSearched, details: new { filters, resultCount = result.TotalCount }),
                cancellationToken);

        await activityLog.RecordViewsAsync(
            ActivityAction.PatientListed,
            result.Items.Select(p => p.Id),
            cancellationToken,
            details: TeamPicker.ListDetails(request.TeamId, request.Teams));

        return PagedMapper.ToResponse(result, request.Page, request.PageSize, PatientMapper.ToResponse);
    }
}
