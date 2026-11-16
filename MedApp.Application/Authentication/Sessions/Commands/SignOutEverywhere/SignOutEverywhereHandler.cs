using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Authentication.Sessions.Commands.SignOutEverywhere;

/// <summary>Ends every session an account has; the activity log records it as an account action, against that account.</summary>
public sealed class SignOutEverywhereHandler(
    ISessionService sessionService,
    IIdentityReadService readService,
    IActivityLog activityLog)
    : IRequestHandler<SignOutEverywhereCommand, bool>
{
    public async Task<bool> Handle(
        SignOutEverywhereCommand request,
        CancellationToken ct)
    {
        // All of them, the admin's own included if they name themselves: that's what everywhere means.
        var found = await sessionService.EndSessionsAsync(readService, request.Username, exceptSessionId: null, ct);

        if (found)
            await activityLog.RecordAsync(
                activityLog.Entry(ActivityAction.SignedOutEverywhere, targetUsername: request.Username),
                ct);

        return found;
    }
}
