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

    /// <summary>
    /// A code as typed or pasted, cleaned the one way every check uses: without any whitespace (spaces people group
    /// digits with, and the line break a pasted code often brings).
    /// </summary>
    public static string Normalise(string? code) =>
        string.Concat((code ?? string.Empty).Where(c => !char.IsWhiteSpace(c)));

    /// <summary>A six-digit code (from the app or an email; whitespace ignored), rather than a recovery code.</summary>
    public static bool IsAuthenticatorCode(string? code)
    {
        var digits = Normalise(code);
        return digits.Length == 6 && digits.All(char.IsAsciiDigit);
    }

    /// <summary>A code from an email that has run out, used up its tries, or was already used.</summary>
    public const string EmailCodeNotLive =
        "That code has run out or had too many tries. Ask for a new one, then enter the code in the newest email.";

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

        bool verified;
        if (IsAuthenticatorCode(code) && method == TwoFactorMethod.Email)
        {
            var check = await emailCodes.CheckAsync(userId, EmailCodePurpose.Confirm, null, code, ct);
            // A code that can't work any more isn't a guess: say to ask for a new one, without counting it.
            if (check == EmailCodeCheck.NotLive)
                throw new ValidationException([new ValidationFailure(field, EmailCodeNotLive)]);
            verified = check == EmailCodeCheck.Right;
        }
        else
        {
            verified = IsAuthenticatorCode(code)
                ? await twoFactor.VerifyAuthenticatorCodeAsync(userId, Normalise(code), ct)
                : allowRecoveryCode && (await twoFactor.RedeemRecoveryCodeAsync(userId, code, ct)).Succeeded;
        }

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
