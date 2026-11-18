using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Queries.GetCalendarStaff;

public sealed class GetCalendarStaffHandler(
    ICalendarRepository repository,
    IIdentityReadService identityReadService,
    IPermissionHolders permissionHolders,
    TimeProvider timeProvider)
    : IRequestHandler<GetCalendarStaffQuery, IReadOnlyList<StaffMemberResponse>>
{
    public async Task<IReadOnlyList<StaffMemberResponse>> Handle(GetCalendarStaffQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<(Guid UserId, string Username)> staff = await identityReadService.GetStaffAsync(cancellationToken);

        // Only the holders of one permission, when asked. The validator refuses a name code doesn't know; were one to
        // get here, it grants nothing, so nobody holds it.
        if (request.Permission is not null)
        {
            var list = staff.ToList();
            IReadOnlySet<Guid> holders = Permissions.TryParse(request.Permission, out var permission)
                ? await permissionHolders.WhoHoldsAsync(list.Select(s => s.UserId).ToList(), permission, cancellationToken)
                : new HashSet<Guid>();
            staff = list.Where(s => holders.Contains(s.UserId));
        }

        var weeks = (await repository.GetStaffWeekTemplatesOnAsync(UkTime.Today(timeProvider), cancellationToken))
            .GroupBy(w => w.StaffUserId)
            .ToDictionary(g => g.Key, g => g.MaxBy(w => w.ValidFrom));

        return staff.Select(s => CalendarMapper.ToResponse(s, weeks.GetValueOrDefault(s.UserId))).ToList();
    }
}
