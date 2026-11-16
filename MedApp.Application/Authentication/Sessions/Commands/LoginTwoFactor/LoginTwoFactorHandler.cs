using MedApp.Application.Activity.Common;
using MedApp.Application.Authentication.TwoFactor.Repositories;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Authentication.Sessions.Commands.LoginTwoFactor;

/// <summary>
/// Finishes signing in to an account with two-factor authentication: a usable challenge (not expired, not used, not out
/// of attempts) and a right code make the session, as the password alone does for other accounts. A wrong code counts
/// against the challenge and towards the account's lockout, as a wrong password does; a locked account is refused, as at
/// the password step. Signing in with a recovery code uses it up, and the account's owner is told by email.
/// <para>
/// The activity log records the sign-in (<see cref="ActivityAction.SignedIn"/>, saying which kind of code), or a refused
/// one (<see cref="ActivityAction.SignInFailed"/>: a wrong code, a locked account, or a challenge that can't be used),
/// with the address it came from. Never the code.
/// </para>
/// </summary>
public sealed class LoginTwoFactorHandler(
    ISignInChallengeRepository challenges,
    ITwoFactorService twoFactor,
    ISessionService sessionService,
    IIdentityReadService readService,
    IActivityLog activityLog,
    ISecurityNotices notices,
    ISecurityEventLog events,
    TimeProvider timeProvider)
    : IRequestHandler<LoginTwoFactorCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginTwoFactorCommand request, CancellationToken ct)
    {
        var challenge = await challenges.FindByTokenHashAsync(SignInChallengeTokens.Hash(request.Challenge), ct);
        if (challenge is null)
            throw new SignInExpiredException();

        var userId = challenge.UserId;
        var usedRecoveryCode = string.IsNullOrWhiteSpace(request.Code);
        var actor = new ActivityActor(userId, await UsernameAsync(userId, ct), request.IpAddress);

        if (!challenge.IsUsable(timeProvider.GetUtcNow().UtcDateTime))
        {
            await RecordFailureAsync(actor, "ChallengeExpired", ct);
            throw new SignInExpiredException();
        }

        if (await twoFactor.LockoutMinutesLeftAsync(userId, ct) is { } lockedFor)
        {
            await challenges.ConsumeAsync(challenge.Id, ct);
            await RecordFailureAsync(actor, "LockedOut", ct);
            throw new AccountLockedOutException(lockedFor);
        }

        RecoveryCodeRedemption? redemption = null;
        bool verified;
        if (usedRecoveryCode)
        {
            redemption = await twoFactor.RedeemRecoveryCodeAsync(userId, request.RecoveryCode ?? string.Empty, ct);
            verified = redemption.Succeeded;
        }
        else
        {
            verified = await twoFactor.VerifyAuthenticatorCodeAsync(userId, request.Code!, ct);
        }

        if (!verified)
        {
            await challenges.RecordFailureAsync(challenge.Id, ct);

            if (await twoFactor.RecordFailedCodeAsync(userId, ct) is { } lockedNow)
            {
                await challenges.ConsumeAsync(challenge.Id, ct);
                await RecordFailureAsync(actor, usedRecoveryCode ? "InvalidRecoveryCodeLockedOut" : "InvalidCodeLockedOut", ct);
                throw new AccountLockedOutException(lockedNow);
            }

            await RecordFailureAsync(actor, usedRecoveryCode ? "InvalidRecoveryCode" : "InvalidCode", ct);
            throw new InvalidTwoFactorCodeException();
        }

        // Single use: of two requests with the same challenge, only the one that deletes it signs in.
        if (!await challenges.ConsumeAsync(challenge.Id, ct))
        {
            await RecordFailureAsync(actor, "ChallengeExpired", ct);
            throw new SignInExpiredException();
        }

        await twoFactor.RecordSignInAsync(userId, ct);

        var session = await sessionService.CreateSessionAsync(userId, request.IpAddress, request.UserAgent, ct);

        await activityLog.RecordAsync(
            activityLog.Entry(
                ActivityAction.SignedIn,
                details: new { secondStep = usedRecoveryCode ? "recoveryCode" : "authenticator" },
                actor: actor),
            ct);

        if (redemption is not null)
        {
            await events.RecordAsync(
                new SecurityEvent(SecurityEventKind.RecoveryCodeUsed, userId, actor.Username,
                    Details: new Dictionary<string, object?> { ["codesLeft"] = redemption.CodesLeft }),
                ct);
            await notices.SendAsync(userId, SecurityNotice.RecoveryCodeUsed, ct, recoveryCodesLeft: redemption.CodesLeft);
        }

        return session;
    }

    private async Task<string> UsernameAsync(Guid userId, CancellationToken ct) =>
        (await readService.GetUsernamesByIdAsync([userId], ct)).GetValueOrDefault(userId) ?? string.Empty;

    /// <summary>A refused second step, by the account the challenge was for, and why (never the code).</summary>
    private Task RecordFailureAsync(ActivityActor actor, string reason, CancellationToken ct) =>
        activityLog.RecordAsync(
            activityLog.Entry(ActivityAction.SignInFailed, details: new { reason, secondStep = true }, actor: actor),
            ct);
}
