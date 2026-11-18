using MedApp.Application.Activity.Common;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Activity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Users.Commands.DeleteUser;

/// <summary>
/// Deletes an account, once the admin has confirmed with their own password and it has no upcoming appointments, unless
/// it's the last active admin (<see cref="ILastAdminGuard"/>). In
/// one transaction with the deletion, it takes the person off their meetings still to come and records the deletion in
/// the activity log, so a failure in either (another admin changing one of those meetings at that moment is a 409)
/// leaves the account, its meetings and the log as they were.
/// </summary>
public sealed class DeleteUserHandler(
    IIdentityUserService userService,
    IIdentityReadService identityReadService,
    IAppointmentRepository appointmentRepository,
    IMeetingRepository meetingRepository,
    IIdentityTransactions transactions,
    TimeProvider timeProvider,
    IActivityLog activityLog,
    ILastAdminGuard lastAdmin)
    : IRequestHandler<DeleteUserCommand, IdentityResult>
{
    public async Task<IdentityResult> Handle(
        DeleteUserCommand request,
        CancellationToken ct)
    {
        // Deleting an account can't be undone, so the admin confirms with their own password first.
        var confirmed = await userService.VerifyPasswordAsync(request.ActorUsername, request.ActorPassword, ct);
        if (!confirmed.Succeeded)
            return confirmed;

        // No appointment may be left with nobody: upcoming ones must be moved or cancelled first.
        var user = await identityReadService.GetUserAsync(request.Username, ct);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (user is not null
            && await appointmentRepository.WhyCantStopBeingStaffAsync(user.UserId, user.Username, now, ct) is { } upcoming)
            return IdentityResult.Failed(upcoming);

        // A retry runs it all again, from the database as it was.
        return await transactions.RunAsync(async () =>
        {
            // The last active admin stays (a 409), checked under the guard's lock in this transaction.
            if (user is not null && await lastAdmin.WouldLeaveNoAdminAsync(user.UserId, ct))
                return IdentityResult.Failed(IdentityErrors.LastAdmin);

            var result = await userService.DeleteUserAsync(request.Username, ct);
            if (!result.Succeeded)
                return result;

            // Meetings involve no patient, so they don't stop it: they just lose the person (and go, if nobody's left).
            if (user is not null)
                await meetingRepository.RemoveAttendeeFromUpcomingAsync(user.UserId, now, ct);

            await activityLog.RecordAsync(
                activityLog.Entry(ActivityAction.AccountDeleted, targetUsername: request.Username),
                ct);
            return result;
        }, ct);
    }
}
