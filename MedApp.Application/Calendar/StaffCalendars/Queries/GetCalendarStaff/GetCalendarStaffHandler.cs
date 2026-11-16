using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Queries.GetCalendarStaff;

public sealed class GetCalendarStaffHandler(
    ICalendarRepository repository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<GetCalendarStaffQuery, IReadOnlyList<StaffMemberResponse>>
{
    public async Task<IReadOnlyList<StaffMemberResponse>> Handle(GetCalendarStaffQuery request, CancellationToken cancellationToken)
    {
        var staff = await identityReadService.GetStaffAsync(cancellationToken);
        var weeks = (await repository.GetStaffWeekTemplatesOnAsync(UkTime.Today(timeProvider), cancellationToken))
            .GroupBy(w => w.StaffUserId)
            .ToDictionary(g => g.Key, g => g.MaxBy(w => w.ValidFrom));

        return staff.Select(s => CalendarMapper.ToResponse(s, weeks.GetValueOrDefault(s.UserId))).ToList();
    }
}
