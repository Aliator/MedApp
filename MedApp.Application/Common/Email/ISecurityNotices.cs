using MedApp.Application.Common.Identity;
using Microsoft.Extensions.Logging;

namespace MedApp.Application.Common.Email;

/// <summary>Tells someone by email about a change to their account (<see cref="SecurityNotice"/>).</summary>
public interface ISecurityNotices
{
    /// <summary>
    /// Emails the notice to the account's confirmed address, or to <paramref name="toAddress"/> when it's given (the
    /// address it changed from, or the one removed). Nothing is sent to an account without a confirmed address. Best
    /// effort, like every email.
    /// </summary>
    Task SendAsync(
        Guid userId,
        SecurityNotice notice,
        CancellationToken ct,
        string? toAddress = null,
        int? recoveryCodesLeft = null);

    /// <summary>
    /// Emails the notice about the admin <paramref name="aboutUsername"/> to every other admin with a confirmed address
    /// (an admin's two-factor turned on, off or reset), so a change nobody expected is seen by someone who can undo it.
    /// </summary>
    Task SendToOtherAdminsAsync(SecurityNotice notice, Guid aboutUserId, string aboutUsername, CancellationToken ct);
}

public sealed class SecurityNotices(
    IAccountEmailService emails,
    ITwoFactorService twoFactor,
    IEmailSender sender,
    EmailLinks links,
    ILogger<SecurityNotices> logger)
    : ISecurityNotices
{
    public async Task SendAsync(
        Guid userId,
        SecurityNotice notice,
        CancellationToken ct,
        string? toAddress = null,
        int? recoveryCodesLeft = null)
    {
        var account = await emails.GetAsync(userId, ct);
        if (account is null)
            return;

        var to = toAddress ?? account.Email;
        if (string.IsNullOrWhiteSpace(to))
        {
            logger.LogDebug("No notice sent to user {UserId}: no confirmed address.", userId);
            return;
        }

        await sender.SendAsync(
            EmailTemplates.Notice(to, account.Username, notice, links.SignIn(), recoveryCodesLeft),
            ct);
    }

    public async Task SendToOtherAdminsAsync(SecurityNotice notice, Guid aboutUserId, string aboutUsername, CancellationToken ct)
    {
        foreach (var admin in await twoFactor.GetAdminContactsAsync(ct))
        {
            if (admin.UserId == aboutUserId)
                continue;

            await sender.SendAsync(
                EmailTemplates.Notice(admin.Email, admin.Username, notice, links.SignIn(), aboutUsername: aboutUsername),
                ct);
        }
    }
}
