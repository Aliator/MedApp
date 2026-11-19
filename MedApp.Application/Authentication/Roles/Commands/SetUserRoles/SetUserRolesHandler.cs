using MedApp.Application.Activity.Common;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Teams.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Activity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Roles.Commands.SetUserRoles;

/// <summary>
/// Sets a user's roles; the activity log records the change, with the roles before and after. Taking away every role
/// (each makes its holder staff) from someone with upcoming Booked appointments is refused, as deleting them is: their
/// calendar would go, leaving the appointments on a calendar nobody can open. It takes them off their meetings still to
/// come. Nobody changes their own roles, and the last active admin keeps Admin (<see cref="ILastAdminGuard"/>, a 409).
/// The roles, the meetings and the log entry are saved in one transaction, so a failure in any (another admin changing
/// one of those meetings at that moment is a 409) leaves all three as they were. Someone in a team can't be given Admin
/// (a 409, <see cref="TeamRules.TeamMemberIsNoAdmin"/>): an admin is in no team.
/// </summary>
public sealed class SetUserRolesHandler(
    IIdentityRoleService roleService,
    IIdentityReadService readService,
    IAppointmentRepository appointmentRepository,
    IMeetingRepository meetingRepository,
    IIdentityTransactions transactions,
    TimeProvider timeProvider,
    IActivityLog activityLog,
    ILastAdminGuard lastAdmin,
    ITeamRepository teams)
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
        var target = await readService.GetUserAsync(request.Username, ct);
        var user = stopsBeingStaff ? target : null;
        if (user is not null
            && await appointmentRepository.WhyCantStopBeingStaffAsync(user.UserId, user.Username, now, ct) is { } upcoming)
            return IdentityResult.Failed(upcoming);

        var before = await readService.GetRolesForUserAsync(request.Username, ct);

        // An admin is in no team (decision 209): someone who is both has two accounts.
        var becomesAdmin = request.Roles.Contains(AppRoles.Admin, StringComparer.Ordinal)
                           && !before.Contains(AppRoles.Admin, StringComparer.Ordinal);
        if (becomesAdmin && target is not null && (await teams.GetForUserAsync(target.UserId, ct)).Count > 0)
            throw new ConflictException(TeamRules.TeamMemberIsNoAdmin(target.Username));

        // SetRolesAsync's own transaction joins this one. A retry runs it all again, from the database as it was.
        var losesAdmin = !request.Roles.Contains(AppRoles.Admin, StringComparer.OrdinalIgnoreCase);
        return await transactions.RunAsync(async () =>
        {
            // Checked under the guard's lock, in this transaction, so two admins can't take Admin from each other at once.
            if (losesAdmin && target is not null && await lastAdmin.WouldLeaveNoAdminAsync(target.UserId, ct))
                return IdentityResult.Failed(IdentityErrors.LastAdmin);

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
