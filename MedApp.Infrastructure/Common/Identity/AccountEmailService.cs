using MedApp.Application.Common.Identity;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Infrastructure.Common.Identity;

/// <summary>
/// Staff email addresses on Identity's <c>Email</c> and <c>EmailConfirmed</c>, with a new address waiting in
/// <see cref="ApplicationUser.PendingEmail"/> until its link is opened. The link's token is Identity's change-email token
/// for that address: sealed with the data protection keys, bound to the address and to the account's security stamp,
/// and valid for 24 hours (<c>DataProtectionTokenProviderOptions</c>). Confirming uses Identity's <c>ChangeEmailAsync</c>,
/// which sets the address as confirmed. Only a confirmed address is ever returned as the account's.
/// </summary>
public sealed class AccountEmailService(UserManager<ApplicationUser> userManager) : IAccountEmailService
{
    public async Task<AccountEmail?> GetAsync(Guid userId, CancellationToken ct)
    {
        var user = await FindAsync(userId);
        return user is null ? null : ToAccountEmail(user);
    }

    public async Task<EmailChangeRequest?> RequestChangeAsync(Guid userId, string email, CancellationToken ct)
    {
        var user = await FindAsync(userId);
        if (user is null)
            return null;

        var username = user.UserName ?? string.Empty;

        // Already the confirmed address: nothing to confirm, and any other address waiting is dropped.
        if (ConfirmedEmail(user) is { } confirmed && string.Equals(confirmed, email, StringComparison.OrdinalIgnoreCase))
        {
            if (user.PendingEmail is not null)
            {
                user.PendingEmail = null;
                await userManager.UpdateAsync(user);
            }

            return new EmailChangeRequest(username, confirmed, null);
        }

        user.PendingEmail = email;
        var saved = await userManager.UpdateAsync(user);
        if (!saved.Succeeded)
            return null;

        var token = await userManager.GenerateChangeEmailTokenAsync(user, email);
        return new EmailChangeRequest(username, email, token);
    }

    public async Task<EmailConfirmation> ConfirmAsync(Guid userId, string token, CancellationToken ct)
    {
        var user = await FindAsync(userId);
        if (user?.PendingEmail is not { } pending)
            return new EmailConfirmation(IdentityResult.Failed(IdentityErrors.InvalidToken));

        var previous = ConfirmedEmail(user);

        // Cleared in the same save as the change, or not at all.
        user.PendingEmail = null;
        var changed = await userManager.ChangeEmailAsync(user, pending, token);
        if (!changed.Succeeded)
        {
            user.PendingEmail = pending;
            return new EmailConfirmation(IdentityResult.Failed(IdentityErrors.InvalidToken));
        }

        return new EmailConfirmation(
            IdentityResult.Success,
            user.UserName,
            previous is not null && !string.Equals(previous, pending, StringComparison.OrdinalIgnoreCase) ? previous : null);
    }

    public async Task<AccountEmail?> RemoveAsync(Guid userId, CancellationToken ct)
    {
        var user = await FindAsync(userId);
        if (user is null)
            return null;

        var before = ToAccountEmail(user);
        if (user.Email is null && user.PendingEmail is null)
            return before;

        // SetEmailAsync saves, with the pending address cleared in the same update.
        user.PendingEmail = null;
        await userManager.SetEmailAsync(user, null);
        return before;
    }

    private Task<ApplicationUser?> FindAsync(Guid userId) =>
        userManager.FindByIdAsync(userId.ToString());

    private static string? ConfirmedEmail(ApplicationUser user) =>
        user.EmailConfirmed && !string.IsNullOrEmpty(user.Email) ? user.Email : null;

    private static AccountEmail ToAccountEmail(ApplicationUser user) =>
        new(user.Id, user.UserName ?? string.Empty, ConfirmedEmail(user), user.PendingEmail);
}
