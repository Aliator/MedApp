using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;

namespace MedApp.Application.Authentication.TwoFactor.Common;

/// <summary>
/// Checking a code a signed-in person types to confirm a two-factor change. A wrong one counts towards the lockout, as
/// at sign-in, so a stolen session can't be used to guess codes; a locked account is refused.
/// </summary>
public static class TwoFactorCodes
{
    public const string WrongCode =
        "That code isn't right. Check that your phone's clock is set automatically, then try the newest code.";

    /// <summary>A six-digit authenticator code (spaces ignored), rather than a recovery code.</summary>
    public static bool IsAuthenticatorCode(string code)
    {
        var digits = code.Replace(" ", string.Empty);
        return digits.Length == 6 && digits.All(char.IsAsciiDigit);
    }

    /// <summary>
    /// Checks an authenticator code (or, with <paramref name="allowRecoveryCode"/>, a recovery code, which it uses up),
    /// throwing a validation error on <paramref name="field"/> for a wrong one and <see cref="AccountLockedOutException"/>
    /// for a locked account.
    /// </summary>
    public static async Task VerifyOrThrowAsync(
        this ITwoFactorService twoFactor,
        Guid userId,
        string code,
        string field,
        bool allowRecoveryCode,
        CancellationToken ct)
    {
        if (await twoFactor.LockoutMinutesLeftAsync(userId, ct) is { } lockedFor)
            throw new AccountLockedOutException(lockedFor);

        var verified = IsAuthenticatorCode(code)
            ? await twoFactor.VerifyAuthenticatorCodeAsync(userId, code, ct)
            : allowRecoveryCode && (await twoFactor.RedeemRecoveryCodeAsync(userId, code, ct)).Succeeded;

        if (verified)
            return;

        if (await twoFactor.RecordFailedCodeAsync(userId, ct) is { } lockedNow)
            throw new AccountLockedOutException(lockedNow);

        throw new ValidationException([new ValidationFailure(field, WrongCode)]);
    }
}
