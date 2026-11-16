using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Users.Commands.UpdateUserPassword;

public sealed class UpdateUserPasswordHandler(
    IIdentityUserService userService,
    IIdentityReadService readService,
    ISessionService sessionService)
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

        // Anyone signed in elsewhere with the old password (perhaps not the user) is signed out; this session stays.
        if (result.Succeeded)
            await sessionService.EndSessionsAsync(readService, request.Username, request.CurrentSessionId, ct);

        return result;
    }
}
