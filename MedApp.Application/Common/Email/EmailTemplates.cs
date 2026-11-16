using System.Net;
using System.Text;

namespace MedApp.Application.Common.Email;

/// <summary>
/// Every email MedApp sends, as plain, accessible HTML (a language, a title, real paragraphs and links, no images or
/// layout tables) and the same words as text. They hold only the account's username, a link and what changed: never
/// clinical information (no patient, procedure or notification text) and never a password or a code. Every value is
/// HTML-encoded.
/// </summary>
public static class EmailTemplates
{
    public const string ConfirmAddressSubject = "Confirm your email address for MedApp";
    public const string ResetLinkSubject = "Reset your MedApp password";

    /// <summary>How long a reset link works, as the emails say it (the token's lifetime is set to match).</summary>
    public static readonly TimeSpan ResetLinkLifetime = TimeSpan.FromMinutes(30);

    /// <summary>How long an email confirmation link works.</summary>
    public static readonly TimeSpan ConfirmLinkLifetime = TimeSpan.FromHours(24);

    private const string NotYou =
        "If this wasn't you, tell your MedApp administrator at once: someone else may be using your account.";

    /// <summary>A link to confirm a new email address, sent to that address.</summary>
    public static EmailMessage ConfirmAddress(string to, string username, string link) =>
        Build(
            to,
            ConfirmAddressSubject,
            $"Hello {username},",
            [
                $"Someone asked to use this email address for the MedApp account {username}. MedApp uses it to send you a link if you forget your password, and to tell you about changes to how you sign in.",
                "To confirm it, open the link below within 24 hours.",
            ],
            ("Confirm this email address", link),
            ["If you didn't ask for this, you can ignore this email: the address won't be used."]);

    /// <summary>A link to choose a new password, sent to the account's confirmed email address.</summary>
    public static EmailMessage ResetLink(string to, string username, string link) =>
        Build(
            to,
            ResetLinkSubject,
            $"Hello {username},",
            [
                $"Someone asked to reset the password of the MedApp account {username}.",
                "To choose a new password, open the link below. It works once, for 30 minutes.",
            ],
            ("Choose a new password", link),
            ["If you didn't ask for this, you can ignore this email: your password hasn't changed."]);

    /// <summary>A notice of a change to the account, sent to its confirmed email address (or the one it changed from).</summary>
    /// <param name="aboutUsername">For a notice to admins about another admin: that admin's username.</param>
    public static EmailMessage Notice(
        string to,
        string username,
        SecurityNotice notice,
        string? signInLink,
        int? recoveryCodesLeft = null,
        string? aboutUsername = null)
    {
        var (subject, what) = notice switch
        {
            SecurityNotice.ResetByLink => (
                "Your MedApp password was reset",
                "The password of your MedApp account was reset with a link sent to this email address. Every session you had has been signed out."),
            SecurityNotice.TwoFactorEnabled => (
                "Two-factor authentication is on for your MedApp account",
                "Two-factor authentication was turned on for your MedApp account. From now on, signing in needs a code from your authenticator app as well as your password."),
            SecurityNotice.TwoFactorDisabled => (
                "Two-factor authentication is off for your MedApp account",
                "Two-factor authentication was turned off for your MedApp account. Signing in now needs only your password."),
            SecurityNotice.TwoFactorReset => (
                "An administrator reset your MedApp two-factor authentication",
                "A MedApp administrator turned off two-factor authentication for your account, so you can set it up again, and signed you out everywhere. Your authenticator app and recovery codes no longer work for it."),
            SecurityNotice.RecoveryCodesRegenerated => (
                "New recovery codes for your MedApp account",
                "New recovery codes were made for your MedApp account. The old ones no longer work."),
            SecurityNotice.RecoveryCodeUsed => (
                "A recovery code was used to sign in to MedApp",
                $"Someone signed in to your MedApp account with one of its recovery codes. Each code works once{(recoveryCodesLeft is { } left ? $", and you have {left} left" : string.Empty)}. If you've lost your authenticator app, set up two-factor authentication again in Account Settings, Security."),
            SecurityNotice.AddressChanged => (
                "Your MedApp email address was changed",
                "The email address of your MedApp account was changed to another one. MedApp won't send email to this address any more."),
            SecurityNotice.AddressRemoved => (
                "Your MedApp email address was removed",
                "This email address was removed from your MedApp account. MedApp won't send email to it any more, and a forgotten password will need an administrator."),
            SecurityNotice.AddressChangeRequested => (
                "Someone asked to change your MedApp email address",
                "Someone signed in to your MedApp account asked to use a different email address for it. This address stays in use until the new one is confirmed."),
            SecurityNotice.TwoFactorCodesLockedOut => (
                "Wrong sign-in codes locked your MedApp account",
                "Someone entered your MedApp password correctly, then too many wrong two-factor codes, so the account is locked for a while. If it wasn't you, they know your password: change it as soon as you can."),
            SecurityNotice.AdminTwoFactorEnabled => (
                "An administrator turned on two-factor authentication",
                $"Two-factor authentication was turned on for the MedApp administrator account {aboutUsername}. You're told because you're an administrator too."),
            SecurityNotice.AdminTwoFactorRemoved => (
                "An administrator's two-factor authentication was turned off",
                $"Two-factor authentication was turned off or reset for the MedApp administrator account {aboutUsername}. You're told because you're an administrator too."),
            _ => throw new ArgumentOutOfRangeException(nameof(notice), notice, "No email for this notice."),
        };

        var message = Build(
            to,
            subject,
            $"Hello {username},",
            [what],
            signInLink is null ? null : ("Open MedApp", signInLink),
            [NotYou]);
        return message with { Category = EmailCategory.Notice };
    }

    private static EmailMessage Build(
        string to,
        string subject,
        string greeting,
        IReadOnlyList<string> paragraphs,
        (string Text, string Url)? link,
        IReadOnlyList<string> closing)
    {
        var text = new StringBuilder();
        text.AppendLine(greeting).AppendLine();
        foreach (var paragraph in paragraphs)
            text.AppendLine(paragraph).AppendLine();
        if (link is { } textLink)
            text.AppendLine($"{textLink.Text}: {textLink.Url}").AppendLine();
        foreach (var paragraph in closing)
            text.AppendLine(paragraph).AppendLine();
        text.AppendLine("MedApp");

        var html = new StringBuilder();
        html.Append("<!doctype html><html lang=\"en-GB\"><head><meta charset=\"utf-8\">");
        html.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.Append($"<title>{Encode(subject)}</title></head>");
        html.Append("<body style=\"margin:0;padding:24px;font-family:Arial,Helvetica,sans-serif;font-size:16px;line-height:1.5;color:#1b1b1b;background:#ffffff\">");
        html.Append($"<p>{Encode(greeting)}</p>");
        foreach (var paragraph in paragraphs)
            html.Append($"<p>{Encode(paragraph)}</p>");
        if (link is { } htmlLink)
        {
            html.Append($"<p><a href=\"{Encode(htmlLink.Url)}\" style=\"color:#0b4f9c;font-weight:bold\">{Encode(htmlLink.Text)}</a></p>");
            html.Append($"<p>If the link doesn't open, copy this address into your browser:<br>{Encode(htmlLink.Url)}</p>");
        }
        foreach (var paragraph in closing)
            html.Append($"<p>{Encode(paragraph)}</p>");
        html.Append("<p>MedApp</p></body></html>");

        return new EmailMessage(to, subject, html.ToString(), text.ToString());
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
