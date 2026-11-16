using MedApp.Application.Activity.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Activity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Users.Commands.DeleteUser;

public sealed class DeleteUserHandler(
    IIdentityUserService userService,
    IIdentityReadService identityReadService,
    IAppointmentRepository appointmentRepository,
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
        if (await identityReadService.GetUserAsync(request.Username, ct) is { } user)
        {
            var upcoming = await appointmentRepository.CountUpcomingForStaffAsync(
                user.UserId, timeProvider.GetUtcNow().UtcDateTime, ct);
            if (upcoming > 0)
                return IdentityResult.Failed(IdentityErrors.HasUpcomingAppointments(user.Username, upcoming));
        }

        var result = await userService.DeleteUserAsync(
            request.Username,
            ct);

        if (result.Succeeded)
            await activityLog.RecordAsync(
                activityLog.Entry(ActivityAction.AccountDeleted, targetUsername: request.Username),
                ct);

        return result;
    }
}
