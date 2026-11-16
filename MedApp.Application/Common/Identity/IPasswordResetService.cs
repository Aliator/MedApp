using System.Text;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Common.Identity;

/// <summary>
/// Resetting a forgotten password with a link sent to the account's confirmed email address. The link's token is
/// Identity's password reset token: sealed with the data protection keys, bound to the account's security stamp (so it
/// stops working once used, or once the password changes another way) and valid for 30 minutes.
/// </summary>
public interface IPasswordResetService
{
    /// <summary>
    /// The accounts a reset request for <paramref name="usernameOrEmail"/> should email, each with a new reset token: the
    /// account with that username, and every account whose confirmed address it is, as long as it has one. Accounts
    /// with no confirmed address aren't included. Empty when nothing matches, which callers must not reveal.
    /// </summary>
    Task<IReadOnlyList<PasswordResetTarget>> FindTargetsAsync(string usernameOrEmail, CancellationToken ct);

    /// <summary>
    /// Sets a new password with a reset token. The token must be valid (a wrong, used or expired one, or no such
    /// account, is <see cref="IdentityErrors.InvalidToken"/>), the password must meet the rules and differ from the
    /// current one (<see cref="IdentityErrors.SamePassword"/>). On success the must-change-password flag is cleared in
    /// the same save, and any lockout ends.
    /// </summary>
    Task<PasswordResetResult> ResetAsync(Guid userId, string token, string newPassword, CancellationToken ct);
}

public sealed record PasswordResetTarget(Guid UserId, string Username, string Email, string Token)
{
    // Never prints the address or the token.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(UserId)} = {UserId}, {nameof(Username)} = {Username}, {nameof(Token)} = ***");
        return true;
    }
}

public sealed record PasswordResetResult(IdentityResult Result, string? Username = null);
