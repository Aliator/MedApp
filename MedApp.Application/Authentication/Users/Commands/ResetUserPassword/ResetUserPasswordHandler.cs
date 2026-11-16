using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Authentication.Users.Commands.ResetUserPassword;

/// <summary>Gives an account a new generated password; the activity log records the reset (never the password).</summary>
public sealed class ResetUserPasswordHandler(
    IIdentityUserService userService,
    IActivityLog activityLog)
    : IRequestHandler<ResetUserPasswordCommand, ResetUserPasswordResponse>
{
    public async Task<ResetUserPasswordResponse> Handle(
        ResetUserPasswordCommand request,
        CancellationToken ct)
    {
        var response = await userService.ResetUserPasswordAsync(
            request.Username,
            ct);

        if (!response.Errors.Any())
            await activityLog.RecordAsync(
                activityLog.Entry(ActivityAction.PasswordReset, targetUsername: request.Username),
                ct);

        return response;
    }
}
