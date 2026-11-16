using MedApp.Application.Common.Email;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MedApp.Application.Authentication.Passwords.Commands.ForgotPassword;

/// <summary>
/// Emails a single-use reset link, valid for 30 minutes, to each account the username or address matches that has a
/// confirmed email address. Nothing tells the caller whether any did: the work is the same quick lookup either way, and
/// the emails go through the background queue, so even the time taken says little. Each request is recorded, with the
/// account when one matched, and never with what was typed.
/// </summary>
public sealed class ForgotPasswordHandler(
    IPasswordResetService resets,
    IEmailSender sender,
    EmailLinks links,
    ISecurityEventLog events,
    ILogger<ForgotPasswordHandler> logger)
    : IRequestHandler<ForgotPasswordCommand>
{
    public async Task Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        var targets = await resets.FindTargetsAsync(request.UsernameOrEmail.Trim(), ct);

        if (targets.Count == 0)
        {
            await events.RecordAsync(
                new SecurityEvent(SecurityEventKind.PasswordResetRequested, null, null,
                    Details: new Dictionary<string, object?> { ["matched"] = false }),
                ct);
            return;
        }

        foreach (var target in targets)
        {
            await events.RecordAsync(
                new SecurityEvent(SecurityEventKind.PasswordResetRequested, target.UserId, target.Username,
                    Details: new Dictionary<string, object?> { ["matched"] = true }),
                ct);

            if (links.ResetPassword(target.UserId, target.Token) is not { } link)
            {
                logger.LogWarning(
                    "No password reset email sent for user {UserId}: Email:LinkBaseUrl isn't set, so there's no link to send.",
                    target.UserId);
                continue;
            }

            await sender.SendAsync(EmailTemplates.ResetPassword(target.Email, target.Username, link), ct);
        }
    }
}
