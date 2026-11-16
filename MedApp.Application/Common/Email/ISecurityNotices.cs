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
}

public sealed class SecurityNotices(
    IAccountEmailService emails,
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
            logger.LogDebug("No {Notice} email for user {UserId}: no confirmed email address.", notice, userId);
            return;
        }

        await sender.SendAsync(
            EmailTemplates.Notice(to, account.Username, notice, links.SignIn(), recoveryCodesLeft),
            ct);
    }
}
