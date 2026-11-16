using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Activity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Users.Commands.UpdateUserPassword;

/// <summary>Changes a user's own password; the activity log records the change.</summary>
public sealed class UpdateUserPasswordHandler(
    IIdentityUserService userService,
    IActivityLog activityLog)
    : IRequestHandler<UpdateUserPasswordCommand, IdentityResult>
{
    public async Task<IdentityResult> Handle(
        UpdateUserPasswordCommand request,
        CancellationToken ct)
    {
        var result = await userService.UpdateUserPasswordAsync(
            request.Username,
            request.OldPassword,
            request.NewPassword,
            ct);

        if (result.Succeeded)
            await activityLog.RecordAsync(
                activityLog.Entry(ActivityAction.PasswordChanged, targetUsername: request.Username),
                ct);

        return result;
    }
}
