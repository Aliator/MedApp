using System.Text;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Common.Identity;

/// <summary>
/// Two-factor authentication with an authenticator app (TOTP, RFC 6238), on ASP.NET Core Identity's authenticator
/// support: Identity keeps each account's key and recovery codes and whether it's on. Codes are checked against the
/// app's clock (<see cref="TimeProvider"/>), allowing one 30-second step either side, and each code is accepted once.
/// </summary>
public interface ITwoFactorService
{
    Task<bool> IsEnabledAsync(Guid userId, CancellationToken ct);

    /// <summary>The account's two-factor state, or null if there's no such account.</summary>
    Task<TwoFactorState?> GetStateAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Gives the account a new authenticator key, for setting up an app: any earlier key stops working. Only while
    /// two-factor is off. Null if there's no such account.
    /// </summary>
    Task<AuthenticatorKey?> ResetAuthenticatorKeyAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Whether <paramref name="code"/> is the account's current authenticator code (one step either side), and hasn't
    /// been accepted before. An accepted code is recorded, so it can't be used again.
    /// </summary>
    Task<bool> VerifyAuthenticatorCodeAsync(Guid userId, string code, CancellationToken ct);

    /// <summary>Uses up one of the account's recovery codes, if <paramref name="code"/> is one (ignoring case and spaces).</summary>
    Task<RecoveryCodeRedemption> RedeemRecoveryCodeAsync(Guid userId, string code, CancellationToken ct);

    /// <summary>Turns two-factor on, with a new set of recovery codes (returned once).</summary>
    Task<RecoveryCodesResult> EnableAsync(Guid userId, CancellationToken ct);

    /// <summary>Replaces the account's recovery codes with a new set (returned once).</summary>
    Task<RecoveryCodesResult> RegenerateRecoveryCodesAsync(Guid userId, CancellationToken ct);

    /// <summary>Turns two-factor off, and forgets the account's key and recovery codes, so the old app and codes stop working.</summary>
    Task<IdentityResult> DisableAsync(Guid userId, CancellationToken ct);

    /// <summary>Minutes left on the account's lockout (at least one), or null if it isn't locked.</summary>
    Task<int?> LockoutMinutesLeftAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Counts a wrong code towards the account's lockout, in one statement (so codes sent together are all counted).
    /// The one that reaches the limit locks it, for longer each time (<c>CodeLockouts</c>). Returns the lockout's minutes
    /// if this call locked it, otherwise null.
    /// </summary>
    Task<int?> RecordFailedCodeAsync(Guid userId, CancellationToken ct);

    /// <summary>Clears the account's count of failures and of code lockouts, after a complete sign-in.</summary>
    Task RecordSignInAsync(Guid userId, CancellationToken ct);

    /// <summary>The usernames of the accounts with the Admin role that don't have two-factor on.</summary>
    Task<IReadOnlyList<string>> GetAdminsWithoutTwoFactorAsync(CancellationToken ct);

    /// <summary>The accounts with the Admin role that have a confirmed email address, to tell about an admin's two-factor.</summary>
    Task<IReadOnlyList<AccountContact>> GetAdminContactsAsync(CancellationToken ct);
}

/// <param name="IsAdmin">Whether the account has the Admin role (which the admin requirement covers).</param>
public sealed record TwoFactorState(string Username, bool Enabled, int RecoveryCodesLeft, bool IsAdmin);

/// <param name="Key">The key, in Base32 as authenticator apps take it.</param>
public sealed record AuthenticatorKey(string Username, string Key)
{
    // Never prints the key.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(Username)} = {Username}, {nameof(Key)} = ***");
        return true;
    }
}

public sealed record RecoveryCodeRedemption(bool Succeeded, int CodesLeft);

/// <summary>An account and its confirmed email address.</summary>
public sealed record AccountContact(Guid UserId, string Username, string Email)
{
    // Never prints the address.
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append($"{nameof(UserId)} = {UserId}, {nameof(Username)} = {Username}");
        return true;
    }
}

public sealed record RecoveryCodesResult(IdentityResult Result, IReadOnlyList<string> Codes)
{
    // Never prints the codes.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(Result)} = {Result}, {nameof(Codes)} = {Codes.Count} codes");
        return true;
    }
}
