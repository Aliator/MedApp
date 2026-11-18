namespace MedApp.Application.Practices.Common;

/// <summary>
/// Settings for letters (the <c>Letters</c> section): which email domains count as secure for a practice's address
/// (letters are sent by email only to those), and the secure email relay MedApp sends letters through
/// (docs/centre-phases-3-5-plan.md, decision 103).
/// </summary>
public sealed class LettersOptions
{
    public const string Section = "Letters";

    /// <summary>The domains used when none are configured: NHSmail's.</summary>
    public static readonly IReadOnlyList<string> DefaultSecureEmailDomains = ["nhs.net"];

    /// <summary>
    /// The email domains a practice's secure address may be in (a subdomain of one counts); <c>nhs.net</c> when none are
    /// set. Left null by default, as configuration adds to a list rather than replacing it.
    /// </summary>
    public string[]? SecureEmailDomains { get; set; }

    /// <summary>The domains in force, in lower case.</summary>
    public IReadOnlyList<string> AllowedSecureEmailDomains =>
        SecureEmailDomains?.Where(d => !string.IsNullOrWhiteSpace(d)).ToList() is { Count: > 0 } domains
            ? domains.Select(d => d.Trim().TrimStart('@').ToLowerInvariant()).ToList()
            : DefaultSecureEmailDomains;

    /// <summary>The relay letters go through by secure email; off unless it's configured and declared.</summary>
    public LetterSecureEmailOptions SecureEmail { get; set; } = new();
}

/// <summary>
/// The secure email relay for letters (<c>Letters:SecureEmail</c>): an NHSmail or other accredited relay, separate from
/// the best-effort email for staff (<c>Email</c>). MedApp sends letters through it only when a host and a from address
/// are set <b>and</b> the deploying organisation has declared that the relay meets the secure email standard
/// (DCB1596: <see cref="RelayMeetsDcb1596"/>). In development it's Mailpit, which keeps everything on the machine.
/// </summary>
public sealed class LetterSecureEmailOptions
{
    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    /// <summary><c>StartTls</c> (the default: TLS required), <c>SslOnConnect</c>, <c>Auto</c>, or <c>None</c> (a local catcher only).</summary>
    public string Security { get; set; } = "StartTls";

    public string? Username { get; set; }

    public string? Password { get; set; }

    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>The organisation's secure mailbox letters are sent from.</summary>
    public string? FromAddress { get; set; }

    /// <summary>The centre's name, as the sender and in the email's words ("A letter from ..."): never a person.</summary>
    public string? FromName { get; set; }

    /// <summary>
    /// The deploying organisation's declaration that the relay meets DCB1596 (secure email). Without it nothing is sent
    /// from MedApp, however the relay is configured: letters are recorded as sent another way instead.
    /// </summary>
    public bool RelayMeetsDcb1596 { get; set; }

    /// <summary>Whether letters can be sent by secure email from MedApp: a host, a from address and the declaration.</summary>
    public bool IsAvailable =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress) && RelayMeetsDcb1596;

    /// <summary>The name the centre signs its emails with.</summary>
    public string CentreName => string.IsNullOrWhiteSpace(FromName) ? "the clinic" : FromName.Trim();
}
