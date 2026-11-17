using MedApp.Application.Activity.Common;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Activity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Roles.Commands.SetUserRoles;

/// <summary>
/// Sets a user's roles; the activity log records the change, with the roles before and after. Taking away every staff
/// role (Admin and User) from someone with upcoming Booked appointments is refused, as deleting them is: their calendar
/// would go, leaving the appointments on a calendar nobody can open. It takes them off their meetings still to come.
/// The roles, the meetings and the log entry are saved in one transaction, so a failure in any (another admin changing
/// one of those meetings at that moment is a 409) leaves all three as they were.
/// </summary>
public sealed class SetUserRolesHandler(
    IIdentityRoleService roleService,
    IIdentityReadService readService,
    IAppointmentRepository appointmentRepository,
    IMeetingRepository meetingRepository,
    IIdentityTransactions transactions,
    TimeProvider timeProvider,
    IActivityLog activityLog)
    : IRequestHandler<SetUserRolesCommand, IdentityResult>
{
    public async Task<IdentityResult> Handle(
        SetUserRolesCommand request,
        CancellationToken ct)
    {
        // An admin could otherwise take away their own Admin role, and with it the way back.
        if (string.Equals(request.Username, request.ActorUsername, StringComparison.OrdinalIgnoreCase))
            return IdentityResult.Failed(IdentityErrors.OwnRoles);

        // No appointment may be left with nobody: someone who stops being staff loses their calendar.
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var stopsBeingStaff = !AppRoles.IncludesStaff(request.Roles);
        var user = stopsBeingStaff ? await readService.GetUserAsync(request.Username, ct) : null;
        if (user is not null
            && await appointmentRepository.WhyCantStopBeingStaffAsync(user.UserId, user.Username, now, ct) is { } upcoming)
            return IdentityResult.Failed(upcoming);

        var before = await readService.GetRolesForUserAsync(request.Username, ct);

        // SetRolesAsync's own transaction joins this one. A retry runs it all again, from the database as it was.
        return await transactions.RunAsync(async () =>
        {
            var result = await roleService.SetRolesAsync(request.Username, request.Roles, ct);
            if (!result.Succeeded)
                return result;

            // Their meetings to come lose them (and go, if nobody's left): meetings are only for staff.
            if (user is not null)
                await meetingRepository.RemoveAttendeeFromUpcomingAsync(user.UserId, now, ct);

            await activityLog.RecordAsync(
                activityLog.Entry(
                    ActivityAction.RolesChanged,
                    targetUsername: request.Username,
                    details: new { from = before, to = request.Roles }),
                ct);
            return result;
        }, ct);
    }
}
