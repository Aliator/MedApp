using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Common.Exceptions;

namespace MedApp.Application.Common.Identity;

/// <summary>
/// Asking for the current password again before a change to how someone signs in (their email address, turning two-factor
/// on or off, new recovery codes), so a session left open on a shared computer can't be turned into the account for good.
/// A wrong password counts towards the lockout, as at sign-in.
/// </summary>
public static class PasswordConfirmation
{
    /// <summary>The field a wrong password is reported on.</summary>
    public const string Field = "Password";

    /// <summary>
    /// Checks <paramref name="password"/> is <paramref name="username"/>'s: a validation error on <see cref="Field"/> if
    /// not, and <see cref="AccountLockedOutException"/> for a locked account.
    /// </summary>
    public static async Task VerifyOrThrowAsync(
        this IIdentityUserService users,
        ITwoFactorService twoFactor,
        Guid userId,
        string username,
        string password,
        CancellationToken ct)
    {
        var checkedPassword = await users.VerifyPasswordAsync(username, password, ct);
        if (checkedPassword.Succeeded)
            return;

        if (checkedPassword.Errors.Any(e => e.Code == IdentityErrors.LockedOutCode)
            && await twoFactor.LockoutMinutesLeftAsync(userId, ct) is { } lockedFor)
            throw new AccountLockedOutException(lockedFor);

        throw new ValidationException([new ValidationFailure(Field, IdentityErrors.PasswordIncorrect.Description)]);
    }
}
