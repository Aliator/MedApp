using MedApp.Application.Activity.Common;
using MedApp.Application.Authentication.TwoFactor.Common;
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
/// (<c>LoginTwoFactorHandler</c>) exchanges, with a code, for the session. For an account using codes by email, the
/// challenge lasts as long as an emailed code (<see cref="SignInChallenge.EmailLifetime"/>), and a code is emailed to the
/// account's confirmed address. The activity log records the sign-in, a refused one with the username tried and why
/// (wrong credentials or a locked account), or that a right password was challenged for a code (the second step records
/// the sign-in itself), with the address it came from.
/// </summary>
public sealed class LoginHandler(
    IAuthenticationService authenticationService,
    ISessionService sessionService,
    IActivityLog activityLog,
    ITwoFactorService twoFactor,
    ISignInChallengeRepository challenges,
    IEmailCodeService emailCodes,
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

        var method = await twoFactor.GetMethodAsync(userId, ct);
        if (method != TwoFactorMethod.None)
        {
            var byEmail = method == TwoFactorMethod.Email;

            // Only the token's hash is stored; the browser keeps the token for the second step.
            var token = SignInChallengeTokens.Generate();
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var challenge = new SignInChallenge(
                userId, SignInChallengeTokens.Hash(token), now, byEmail ? SignInChallenge.EmailLifetime : null);
            await challenges.AddAsync(challenge, now, ct);

            await activityLog.RecordAsync(
                activityLog.Entry(
                    ActivityAction.TwoFactorChallenged,
                    details: byEmail ? new { method = "email" } : null,
                    actor: actor),
                ct);

            if (!byEmail)
                return LoginOutcome.TwoFactorRequired(token, challenge.ExpiresAtUtc);

            // Null when the account has no confirmed address to send to (it can still sign in with a recovery code).
            var sent = await emailCodes.SendAsync(userId, EmailCodePurpose.SignIn, challenge.Id, ct);
            return LoginOutcome.TwoFactorRequired(
                token,
                challenge.ExpiresAtUtc,
                TwoFactorMethod.Email,
                sent is null ? null : TwoFactorMapper.ToResponse(sent, now));
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
