using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Authentication;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Authentication.Sessions.Commands.Logout;

/// <summary>Ends the session. The activity log records the sign-out when it's by someone still signed in.</summary>
public sealed class LogoutHandler(
    ISessionService sessionService,
    IActivityLog activityLog)
    : IRequestHandler<LogoutCommand>
{
    public async Task Handle(
        LogoutCommand request,
        CancellationToken ct)
    {
        if (request.SessionId is null)
            return;

        await sessionService.RevokeSessionAsync(
            request.SessionId.Value,
            ct);

        if (activityLog.Actor.UserId is not null)
            await activityLog.RecordAsync(activityLog.Entry(ActivityAction.SignedOut), ct);
    }
}
