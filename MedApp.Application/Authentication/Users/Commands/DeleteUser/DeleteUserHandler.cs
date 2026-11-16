using MedApp.Application.Activity.Common;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Activity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Users.Commands.DeleteUser;

public sealed class DeleteUserHandler(
    IIdentityUserService userService,
    IIdentityReadService identityReadService,
    IAppointmentRepository appointmentRepository,
    IMeetingRepository meetingRepository,
    TimeProvider timeProvider,
    IActivityLog activityLog)
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

        var result = await userService.DeleteUserAsync(
            request.Username,
            ct);

        if (result.Succeeded)
        {
            // Meetings involve no patient, so they don't stop it: they just lose the person (and go, if nobody's left).
            if (user is not null)
                await meetingRepository.RemoveAttendeeFromUpcomingAsync(user.UserId, now, ct);

            await activityLog.RecordAsync(
                activityLog.Entry(ActivityAction.AccountDeleted, targetUsername: request.Username),
                ct);
        }

        return result;
    }
}
