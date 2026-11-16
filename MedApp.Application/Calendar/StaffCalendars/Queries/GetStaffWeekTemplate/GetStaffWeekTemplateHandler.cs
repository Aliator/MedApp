using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Queries.GetStaffWeekTemplate;

public sealed class GetStaffWeekTemplateHandler(
    ICalendarRepository repository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<GetStaffWeekTemplateQuery, StaffWeekTemplateResponse>
{
    public async Task<StaffWeekTemplateResponse> Handle(GetStaffWeekTemplateQuery request, CancellationToken cancellationToken)
    {
        await identityReadService.GetUsernameOrThrowAsync(request.StaffUserId, cancellationToken);

        var defaultWeek = await repository.GetStaffWeekTemplateOnAsync(
            request.StaffUserId, UkTime.Today(timeProvider), cancellationToken);

        return CalendarMapper.ToResponse(request.StaffUserId, defaultWeek);
    }
}
