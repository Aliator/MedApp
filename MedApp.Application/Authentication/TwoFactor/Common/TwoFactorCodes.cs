using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MedApp.Domain.Authentication;

namespace MedApp.Application.Authentication.TwoFactor.Common;

/// <summary>
/// Checking a code a signed-in person types to confirm a two-factor change: from their authenticator app, or, for an
/// account using codes by email, the one emailed to confirm it (<see cref="EmailCodePurpose.Confirm"/>). It counts
/// against the account's limit on codes (shared with sign-in), and a wrong one counts towards the lockout in one
/// statement, as at sign-in, so a stolen session can't be used to guess codes, one at a time or all at once; a locked
/// account is refused, and a lockout is emailed to the holder.
/// </summary>
public static class TwoFactorCodes
{
    public const string WrongCode =
        "That code isn't right. Check that your phone's clock is set automatically, then try the newest code.";

    public const string WrongEmailCode =
        "That code isn't right. Use the code in the newest email from MedApp, or ask for a new one.";

    /// <summary>The message for a wrong code from <paramref name="method"/>.</summary>
    public static string WrongCodeFor(TwoFactorMethod method) =>
        method == TwoFactorMethod.Email ? WrongEmailCode : WrongCode;

    /// <summary>A six-digit authenticator code (spaces ignored), rather than a recovery code.</summary>
    public static bool IsAuthenticatorCode(string code)
    {
        var digits = code.Replace(" ", string.Empty);
        return digits.Length == 6 && digits.All(char.IsAsciiDigit);
    }

    /// <summary>
    /// Checks a 6-digit code from the account's <paramref name="method"/> (the app's current code, or the code emailed to
    /// confirm a change, which it uses up), or, with <paramref name="allowRecoveryCode"/>, a recovery code (which it uses
    /// up too), throwing a validation error on <paramref name="field"/> for a wrong one and
    /// <see cref="AccountLockedOutException"/> for a locked account.
    /// </summary>
    public static async Task VerifyOrThrowAsync(
        this ITwoFactorService twoFactor,
        IEmailCodeService emailCodes,
        IAccountAttemptLimiter attempts,
        ISecurityNotices notices,
        Guid userId,
        TwoFactorMethod method,
        string code,
        string field,
        bool allowRecoveryCode,
        CancellationToken ct)
    {
        if (await twoFactor.LockoutMinutesLeftAsync(userId, ct) is { } lockedFor)
            throw new AccountLockedOutException(lockedFor);

        attempts.Attempt(AccountAttempt.TwoFactorCode, userId);

        var verified = IsAuthenticatorCode(code)
            ? method == TwoFactorMethod.Email
                ? await emailCodes.CheckAsync(userId, EmailCodePurpose.Confirm, null, code, ct)
                : await twoFactor.VerifyAuthenticatorCodeAsync(userId, code, ct)
            : allowRecoveryCode && (await twoFactor.RedeemRecoveryCodeAsync(userId, code, ct)).Succeeded;

        if (verified)
            return;

        if (await twoFactor.RecordFailedCodeAsync(userId, ct) is { } lockedNow)
        {
            await notices.SendAsync(userId, SecurityNotice.TwoFactorCodesLockedOut, ct);
            throw new AccountLockedOutException(lockedNow);
        }

        throw new ValidationException([new ValidationFailure(field, WrongCodeFor(method))]);
    }
}
