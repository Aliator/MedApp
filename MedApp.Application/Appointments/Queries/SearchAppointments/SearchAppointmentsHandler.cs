using MedApp.Application.Activity.Common;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Paging;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Contracts.Common;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Appointments.Queries.SearchAppointments;

/// <summary>
/// A page of appointments. Each patient on the page is recorded as listed (once per person every few minutes). The Did
/// not attend view gives each how many appointments were missed on its pathway.
/// </summary>
public sealed class SearchAppointmentsHandler(
    IAppointmentRepository repository,
    IIdentityReadService identityReadService,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<SearchAppointmentsQuery, PagedResponse<AppointmentResponse>>
{
    public async Task<PagedResponse<AppointmentResponse>> Handle(SearchAppointmentsQuery request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var result = await repository.SearchAsync(request, cancellationToken);
        await activityLog.RecordViewsAsync(ActivityAction.PatientListed, result.Items.Select(a => a.PatientId), cancellationToken);
        var items = await AppointmentMapper.ToResponsesAsync(result.Items, identityReadService, now, cancellationToken);

        if (AppointmentSearch.IsDidNotAttendView(request.View) && result.Items.Count > 0)
        {
            var missed = await repository.CountMissedAsync(result.Items.ToList(), cancellationToken);
            foreach (var item in items)
                item.MissedOnPathwayCount = missed.GetValueOrDefault(item.Id);
        }

        return PagedMapper.ToResponse(new PagedResult<AppointmentResponse>(items, result.TotalCount), request.Page, request.PageSize);
    }
}
