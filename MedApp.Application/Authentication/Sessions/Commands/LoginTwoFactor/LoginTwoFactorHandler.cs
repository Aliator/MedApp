using MedApp.Application.Authentication.TwoFactor.Repositories;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MediatR;

namespace MedApp.Application.Authentication.Sessions.Commands.LoginTwoFactor;

/// <summary>
/// Finishes signing in to an account with two-factor authentication: a usable challenge (not expired, not used, not out
/// of attempts) and a right code make the session, as the password alone does for other accounts. A wrong code counts
/// against the challenge and towards the account's lockout, as a wrong password does; a locked account is refused, as at
/// the password step. Signing in with a recovery code uses it up, and the account's owner is told by email.
/// </summary>
public sealed class LoginTwoFactorHandler(
    ISignInChallengeRepository challenges,
    ITwoFactorService twoFactor,
    ISessionService sessionService,
    ISecurityNotices notices,
    ISecurityEventLog events,
    TimeProvider timeProvider)
    : IRequestHandler<LoginTwoFactorCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginTwoFactorCommand request, CancellationToken ct)
    {
        var challenge = await challenges.FindByTokenHashAsync(SignInChallengeTokens.Hash(request.Challenge), ct);
        if (challenge is null || !challenge.IsUsable(timeProvider.GetUtcNow().UtcDateTime))
            throw new SignInExpiredException();

        var userId = challenge.UserId;

        if (await twoFactor.LockoutMinutesLeftAsync(userId, ct) is { } lockedFor)
        {
            await challenges.ConsumeAsync(challenge.Id, ct);
            throw new AccountLockedOutException(lockedFor);
        }

        var usedRecoveryCode = string.IsNullOrWhiteSpace(request.Code);
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
            await events.RecordAsync(
                new SecurityEvent(SecurityEventKind.TwoFactorCodeRejected, userId, null,
                    Details: new Dictionary<string, object?> { ["recoveryCode"] = usedRecoveryCode }),
                ct);

            if (await twoFactor.RecordFailedCodeAsync(userId, ct) is { } lockedNow)
            {
                await challenges.ConsumeAsync(challenge.Id, ct);
                throw new AccountLockedOutException(lockedNow);
            }

            throw new InvalidTwoFactorCodeException();
        }

        // Single use: of two requests with the same challenge, only the one that deletes it signs in.
        if (!await challenges.ConsumeAsync(challenge.Id, ct))
            throw new SignInExpiredException();

        await twoFactor.RecordSignInAsync(userId, ct);

        var session = await sessionService.CreateSessionAsync(userId, request.IpAddress, request.UserAgent, ct);

        if (redemption is not null)
        {
            await events.RecordAsync(
                new SecurityEvent(SecurityEventKind.RecoveryCodeUsed, userId, null,
                    Details: new Dictionary<string, object?> { ["codesLeft"] = redemption.CodesLeft }),
                ct);
            await notices.SendAsync(userId, SecurityNotice.RecoveryCodeUsed, ct, recoveryCodesLeft: redemption.CodesLeft);
        }

        return session;
    }
}
