using MedApp.Application.Activity.Common;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Paging;
using MedApp.Application.Common.Teams;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Contracts.Common;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Appointments.Queries.SearchAppointments;

/// <summary>
/// A page of appointments the viewer sees (the team walls), optionally one team's (the clinic picker: another team than
/// their own is a 400, unless they see every team). Each patient on the page is recorded as listed (once per person
/// every few minutes; an admin's with <c>access</c> and <c>teamFilter</c>). The Did not attend view gives each how many
/// appointments were missed on its pathway.
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
        TeamPicker.EnsureCanPick(request.TeamId, request.Teams);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var result = await repository.SearchAsync(request, cancellationToken);
        await activityLog.RecordViewsAsync(
            ActivityAction.PatientListed, result.Items.Select(a => a.PatientId), cancellationToken,
            details: TeamPicker.ListDetails(request.TeamId, request.Teams));
        var items = await AppointmentMapper.ToResponsesAsync(result.Items, identityReadService, now, cancellationToken, request.Teams);

        if (AppointmentSearch.IsDidNotAttendView(request.View) && result.Items.Count > 0)
        {
            var missed = await repository.CountMissedAsync(result.Items.ToList(), cancellationToken);
            foreach (var item in items)
                item.MissedOnPathwayCount = missed.GetValueOrDefault(item.Id);
        }

        return PagedMapper.ToResponse(new PagedResult<AppointmentResponse>(items, result.TotalCount), request.Page, request.PageSize);
    }
}
