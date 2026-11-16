using MedApp.Application.Activity.Common;
using MedApp.Application.Authentication.TwoFactor.Repositories;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Activity;
using MedApp.Domain.Authentication;
using MediatR;

namespace MedApp.Application.Authentication.Sessions.Commands.Login;

/// <summary>
/// Checks the password. An account without two-factor authentication gets a session, as it always has. One with it
/// gets no session yet: a short-lived, single-use challenge (<see cref="SignInChallenge"/>), which the second step
/// (<c>LoginTwoFactorHandler</c>) exchanges, with a code, for the session. The activity log records the sign-in, a
/// refused one with the username tried and why (wrong credentials or a locked account), or that a right password was
/// challenged for a code (the second step records the sign-in itself), with the address it came from.
/// </summary>
public sealed class LoginHandler(
    IAuthenticationService authenticationService,
    ISessionService sessionService,
    IActivityLog activityLog,
    ITwoFactorService twoFactor,
    ISignInChallengeRepository challenges,
    TimeProvider timeProvider)
    : IRequestHandler<LoginCommand, LoginOutcome>
{
    public async Task<LoginOutcome> Handle(LoginCommand request, CancellationToken ct)
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
        catch (InvalidCredentialsException refused)
        {
            await RecordFailureAsync(request, "InvalidCredentials", refused.UserId, ct);
            throw;
        }
        catch (AccountLockedOutException refused)
        {
            await RecordFailureAsync(request, "LockedOut", refused.UserId, ct);
            throw;
        }

        var actor = new ActivityActor(userId, username, request.IpAddress);

        if (await twoFactor.IsEnabledAsync(userId, ct))
        {
            // Only the token's hash is stored; the browser keeps the token for the second step.
            var token = SignInChallengeTokens.Generate();
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var challenge = new SignInChallenge(userId, SignInChallengeTokens.Hash(token), now);
            await challenges.AddAsync(challenge, now, ct);

            await activityLog.RecordAsync(activityLog.Entry(ActivityAction.TwoFactorChallenged, actor: actor), ct);

            return LoginOutcome.TwoFactorRequired(token, challenge.ExpiresAtUtc);
        }

        var session = await sessionService.CreateSessionAsync(
            userId,
            request.IpAddress,
            request.UserAgent,
            ct);

        await activityLog.RecordAsync(activityLog.Entry(ActivityAction.SignedIn, actor: actor), ct);

        return LoginOutcome.SignedIn(session);
    }

    /// <summary>A refused sign-in, by the account tried when it exists (so filtering by user finds it), or by the username typed.</summary>
    private Task RecordFailureAsync(LoginCommand request, string reason, Guid? userId, CancellationToken ct) =>
        activityLog.RecordAsync(
            activityLog.Entry(
                ActivityAction.SignInFailed,
                details: new { reason },
                actor: new ActivityActor(userId, request.Username, request.IpAddress)),
            ct);
}
