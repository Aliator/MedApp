using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Users.Commands.ResetUserPassword;

public sealed class ResetUserPasswordHandler(
    IIdentityUserService userService,
    IIdentityReadService readService,
    ISessionService sessionService)
    : IRequestHandler<ResetUserPasswordCommand, ResetUserPasswordResponse>
{
    public async Task<ResetUserPasswordResponse> Handle(
        ResetUserPasswordCommand request,
        CancellationToken ct)
    {
        var response = await userService.ResetUserPasswordAsync(
            request.Username,
            ct);

        // A reset is for a forgotten or exposed password: every session the user had ends, wherever it is, and they
        // sign in again with the new one.
        if (response.GeneratedPassword is not null)
            await sessionService.EndSessionsAsync(readService, request.Username, exceptSessionId: null, ct);

        return response;
    }
}
