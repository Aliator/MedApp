namespace MedApp.Application.Practices.Common;

/// <summary>
/// Settings for letters (the <c>Letters</c> section). So far only which email domains count as secure for a practice's
/// address: letters will be sent only to those (docs/centre-phases-3-5-plan.md, decision 103).
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
}
