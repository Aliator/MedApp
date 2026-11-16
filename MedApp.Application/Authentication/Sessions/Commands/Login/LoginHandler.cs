using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Authentication.Sessions.Commands.Login;

/// <summary>
/// Signs in, starting a session. The activity log records the sign-in, or a refused one with the username tried and
/// why (wrong credentials or a locked account), with the address it came from.
/// </summary>
public sealed class LoginHandler(
    IAuthenticationService authenticationService,
    ISessionService sessionService,
    IActivityLog activityLog)
    : IRequestHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken ct)
    {
        Guid userId;
        string username;
        try
        {
            (userId, username, _) =
                await authenticationService.ValidateCredentialsAsync(
                    request.Username,
                    request.Password);
        }
        catch (InvalidCredentialsException)
        {
            await RecordFailureAsync(request, "InvalidCredentials", ct);
            throw;
        }
        catch (AccountLockedOutException)
        {
            await RecordFailureAsync(request, "LockedOut", ct);
            throw;
        }

        var session = await sessionService.CreateSessionAsync(
            userId,
            request.IpAddress,
            request.UserAgent,
            ct);

        await activityLog.RecordAsync(
            activityLog.Entry(ActivityAction.SignedIn, actor: new ActivityActor(userId, username, request.IpAddress)),
            ct);

        return session;
    }

    private Task RecordFailureAsync(LoginCommand request, string reason, CancellationToken ct) =>
        activityLog.RecordAsync(
            activityLog.Entry(
                ActivityAction.SignInFailed,
                details: new { reason },
                actor: new ActivityActor(null, request.Username, request.IpAddress)),
            ct);
}
