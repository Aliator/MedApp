using System.Net;
using System.Text;

namespace MedApp.Application.Common.Email;

/// <summary>
/// Every email MedApp sends, as plain, accessible HTML (a language, a title, real paragraphs and links, no images or
/// layout tables) and the same words as text. They hold only the account's username, a link and what changed: never
/// clinical information (no patient, pathway or notification text), and never a password, an authenticator key or
/// recovery codes. The one secret an email may carry is a short-lived, single-use two-factor code
/// (<see cref="TwoFactorCode"/>), with nothing else in that email but what it's for, how long it lasts and what to do if
/// you didn't ask for it. Every value is HTML-encoded.
/// </summary>
public static class EmailTemplates
{
    public const string ConfirmAddressSubject = "Confirm your email address for MedApp";
    public const string ResetLinkSubject = "Reset your MedApp password";

    /// <summary>The subject of every two-factor code email. The code itself is never in a subject.</summary>
    public const string TwoFactorCodeSubject = "Your MedApp code";

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

    /// <summary>How much less than a whole minute still counts as it, in <see cref="TwoFactorCode"/>.</summary>
    private static readonly TimeSpan LifetimeSlack = TimeSpan.FromSeconds(5);

    /// <summary>
    /// A two-factor code, sent to the account's confirmed address: the code, what it's for, how long it works, and what to
    /// do if you didn't ask for it. Nothing else: no username, no link, no clinical information.
    /// </summary>
    /// <param name="code">The 6-digit code.</param>
    /// <param name="forSignIn">True for signing in; false for turning codes by email on or confirming a change.</param>
    /// <param name="lifetime">How long it works: <c>EmailCode.Lifetime</c>, or less for a sign-in that ends sooner.</param>
    public static EmailMessage TwoFactorCode(string to, string code, bool forSignIn, TimeSpan lifetime)
    {
        // Whole minutes, rounded down (never promising more than is left), and at least one. A few seconds' slack: a
        // sign-in's code ends with its challenge, made a moment (a few database calls) before the code, so a full
        // 10 minutes arrives as 9:59.9 and still says 10.
        var minutes = Math.Max(1, (int)Math.Floor((lifetime + LifetimeSlack).TotalMinutes));
        var purpose = forSignIn ? "to sign in to MedApp" : "to confirm a change to how you sign in to MedApp";
        var message = Build(
            to,
            TwoFactorCodeSubject,
            greeting: null,
            [
                $"Your code {purpose} is:",
                code,
                $"It works once, for {minutes} {(minutes == 1 ? "minute" : "minutes")}. Don't give it to anyone.",
            ],
            link: null,
            [
                "If you didn't ask for a code, someone may know your password. Don't use the code: change your password as soon as you can, and tell your MedApp administrator.",
            ],
            emphasised: 1);
        return message with { Category = EmailCategory.Code };
    }

    /// <summary>A notice of a change to the account, sent to its confirmed email address (or the one it changed from).</summary>
    /// <param name="aboutUsername">For a notice to admins about another admin: that admin's username.</param>
    /// <param name="detail">For a role change: what that admin did, as a sentence ("changed the role Nurse: …").</param>
    public static EmailMessage Notice(
        string to,
        string username,
        SecurityNotice notice,
        string? signInLink,
        int? recoveryCodesLeft = null,
        string? aboutUsername = null,
        string? detail = null)
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
            SecurityNotice.TwoFactorSwitchedToApp => (
                "Your MedApp sign-in codes moved to an authenticator app",
                "Your MedApp account now signs in with codes from an authenticator app instead of codes by email. MedApp won't email you sign-in codes any more, and your old recovery codes no longer work."),
            SecurityNotice.AdminTwoFactorSwitchedToApp => (
                "An administrator moved their sign-in codes to an authenticator app",
                $"The MedApp administrator account {aboutUsername} now signs in with an authenticator app instead of codes by email. You're told because you're an administrator too."),
            SecurityNotice.EmailCodesEnabled => (
                "Sign-in codes by email are on for your MedApp account",
                "Two-factor authentication with codes by email was turned on for your MedApp account. From now on, signing in needs a code MedApp emails to this address as well as your password."),
            SecurityNotice.TwoFactorReset => (
                "An administrator reset your MedApp two-factor authentication",
                "A MedApp administrator turned off two-factor authentication for your account, so you can set it up again, and signed you out everywhere. Your authenticator app or codes by email, and your recovery codes, no longer work for it."),
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
            SecurityNotice.AdminChangedRole => (
                "An administrator changed a MedApp role",
                $"The MedApp administrator {aboutUsername} {detail} Roles decide what everyone who holds them can do. You're told because you're an administrator too."),
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

    /// <param name="greeting">The first line ("Hello …"), or none.</param>
    /// <param name="emphasised">The index of a paragraph to show large and bold in the HTML (a code), or none.</param>
    private static EmailMessage Build(
        string to,
        string subject,
        string? greeting,
        IReadOnlyList<string> paragraphs,
        (string Text, string Url)? link,
        IReadOnlyList<string> closing,
        int? emphasised = null)
    {
        var text = new StringBuilder();
        if (greeting is not null)
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
        if (greeting is not null)
            html.Append($"<p>{Encode(greeting)}</p>");
        for (var i = 0; i < paragraphs.Count; i++)
            html.Append(i == emphasised
                ? $"<p style=\"font-size:28px;font-weight:bold;letter-spacing:4px\">{Encode(paragraphs[i])}</p>"
                : $"<p>{Encode(paragraphs[i])}</p>");
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
