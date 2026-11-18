using MedApp.Application.Activity.Common;
using MedApp.Application.Authentication.TwoFactor.Common;
using MedApp.Application.Authentication.TwoFactor.Repositories;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MedApp.Domain.Activity;
using MedApp.Domain.Authentication;
using MediatR;

namespace MedApp.Application.Authentication.Sessions.Commands.LoginTwoFactor;

/// <summary>
/// Finishes signing in to an account with two-factor authentication: a usable challenge (not expired, not used, not out
/// of attempts) and a right code make the session, as the password alone does for other accounts. The code is from the
/// account's method: its authenticator app, or the one emailed for this challenge (used up when it's accepted); or a
/// recovery code either way. A wrong emailed code counts exactly as a wrong authenticator code does. A wrong code counts
/// against the challenge and towards the account's lockout, as a wrong password does; a locked account is refused, as at
/// the password step. Signing in with a recovery code uses it up, and the account's owner is told by email.
/// <para>
/// Codes sent together are counted together: each first passes the account's limit across challenges
/// (<see cref="IAccountAttemptLimiter"/>) and reserves one of the challenge's attempts in one statement, and each wrong
/// one is counted towards the lockout in one statement, so a burst gets no more guesses than the same codes one by one.
/// A lockout from codes, which grows each time, is emailed to the account holder.
/// </para>
/// <para>
/// The activity log records the sign-in (<see cref="ActivityAction.SignedIn"/>, saying which kind of code:
/// <c>authenticator</c>, <c>email</c> or <c>recoveryCode</c>), or a refused
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
    IAccountAttemptLimiter attempts,
    IEmailCodeService emailCodes,
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

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (!challenge.IsUsable(now))
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

        // At most so many codes for the account across every challenge (a 429), then one of this challenge's attempts,
        // reserved in one statement before the code is checked: codes sent together can't all slip past the limit.
        try
        {
            attempts.Attempt(AccountAttempt.TwoFactorCode, userId);
        }
        catch (TooManyAttemptsException)
        {
            await RecordFailureAsync(actor, "TooManyCodes", ct);
            throw;
        }

        if (!await challenges.TryReserveAttemptAsync(challenge.Id, now, ct))
        {
            await RecordFailureAsync(actor, "ChallengeExpired", ct);
            throw new SignInExpiredException();
        }

        RecoveryCodeRedemption? redemption = null;
        bool verified;
        var byEmail = false;
        if (usedRecoveryCode)
        {
            redemption = await twoFactor.RedeemRecoveryCodeAsync(userId, request.RecoveryCode ?? string.Empty, ct);
            verified = redemption.Succeeded;
        }
        else if (await twoFactor.GetMethodAsync(userId, ct) == TwoFactorMethod.Email)
        {
            byEmail = true;
            verified = await emailCodes.CheckAsync(userId, EmailCodePurpose.SignIn, challenge.Id, request.Code!, ct);
        }
        else
        {
            verified = await twoFactor.VerifyAuthenticatorCodeAsync(userId, request.Code!, ct);
        }

        if (!verified)
        {
            if (await twoFactor.RecordFailedCodeAsync(userId, ct) is { } lockedNow)
            {
                await challenges.ConsumeAsync(challenge.Id, ct);
                await RecordFailureAsync(actor, usedRecoveryCode ? "InvalidRecoveryCodeLockedOut" : "InvalidCodeLockedOut", ct);
                // Whoever it was knew the password: the holder should know too.
                await notices.SendAsync(userId, SecurityNotice.TwoFactorCodesLockedOut, ct);
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
                details: new { secondStep = usedRecoveryCode ? "recoveryCode" : byEmail ? "email" : "authenticator" },
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
