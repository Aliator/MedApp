using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Identity;
using MediatR;

namespace MedApp.Application.Authentication.Sessions.Commands.SignOutEverywhere;

public sealed class SignOutEverywhereHandler(
    ISessionService sessionService,
    IIdentityReadService readService)
    : IRequestHandler<SignOutEverywhereCommand, bool>
{
    public async Task<bool> Handle(
        SignOutEverywhereCommand request,
        CancellationToken ct)
    {
        // All of them, the admin's own included if they name themselves: that's what everywhere means.
        return await sessionService.EndSessionsAsync(readService, request.Username, exceptSessionId: null, ct);
    }
}
