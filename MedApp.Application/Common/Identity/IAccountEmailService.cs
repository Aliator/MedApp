using System.Text;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Common.Identity;

/// <summary>
/// A staff account's email address. It's optional. A new one waits (pending) until the link sent to it is opened, and
/// only a confirmed one is used: for password reset links and security notices.
/// </summary>
public interface IAccountEmailService
{
    /// <summary>The account's username and addresses, or null if there's no such account.</summary>
    Task<AccountEmail?> GetAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Asks to use <paramref name="email"/>: it becomes the pending address, and the result carries a token that
    /// confirms it (for the emailed link). If it's already the confirmed address, any pending one is dropped and there's
    /// nothing to confirm (no token). Null if there's no such account.
    /// </summary>
    Task<EmailChangeRequest?> RequestChangeAsync(Guid userId, string email, CancellationToken ct);

    /// <summary>
    /// Confirms the pending address with the token from its link: it becomes the account's address. A wrong or expired
    /// token, no pending address, or no such account all fail the same way (<see cref="IdentityErrors.InvalidToken"/>).
    /// </summary>
    Task<EmailConfirmation> ConfirmAsync(Guid userId, string token, CancellationToken ct);

    /// <summary>Removes the account's address and any pending one. Returns them as they were, or null if there's no such account.</summary>
    Task<AccountEmail?> RemoveAsync(Guid userId, CancellationToken ct);
}

/// <param name="Email">The confirmed address, the one MedApp sends to; null if none.</param>
/// <param name="PendingEmail">An address waiting to be confirmed; null if none.</param>
public sealed record AccountEmail(Guid UserId, string Username, string? Email, string? PendingEmail);

/// <param name="Token">Confirms <paramref name="Email"/>; null when it's already the confirmed address.</param>
public sealed record EmailChangeRequest(string Username, string Email, string? Token)
{
    // Never prints the token.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(Username)} = {Username}, {nameof(Token)} = {(Token is null ? "null" : "***")}");
        return true;
    }
}

/// <param name="PreviousEmail">The confirmed address it replaced, to tell about the change; null if none.</param>
public sealed record EmailConfirmation(IdentityResult Result, string? Username = null, string? PreviousEmail = null);
