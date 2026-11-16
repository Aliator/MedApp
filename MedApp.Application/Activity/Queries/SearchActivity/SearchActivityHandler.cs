using MedApp.Application.Activity.Common;
using MedApp.Application.Activity.Repositories;
using MedApp.Application.Common.Paging;
using MedApp.Application.Patients.Repositories;
using MedApp.Contracts.Activity.Responses;
using MedApp.Contracts.Common;
using MediatR;

namespace MedApp.Application.Activity.Queries.SearchActivity;

/// <summary>A page of the activity log, with each entry's patient named as they are now (the log holds only IDs).</summary>
public sealed class SearchActivityHandler(IActivityRepository repository, IPatientRepository patients)
    : IRequestHandler<SearchActivityQuery, PagedResponse<ActivityEntryResponse>>
{
    public async Task<PagedResponse<ActivityEntryResponse>> Handle(SearchActivityQuery request, CancellationToken cancellationToken)
    {
        var result = await repository.SearchAsync(request, cancellationToken);

        var patientIds = result.Items.Select(e => e.PatientId).OfType<Guid>().Distinct().ToList();
        var names = patientIds.Count > 0
            ? await patients.GetNamesAsync(patientIds, cancellationToken)
            : new Dictionary<Guid, string>();

        return PagedMapper.ToResponse(result, request.Page, request.PageSize, e => ActivityEntryMapper.ToResponse(e, names));
    }
}
