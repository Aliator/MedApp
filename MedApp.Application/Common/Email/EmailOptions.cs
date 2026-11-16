namespace MedApp.Application.Common.Email;

/// <summary>
/// The "Email" configuration section's settings that emails' contents need. The mail server's own settings are under
/// <c>Email:Smtp</c> (Infrastructure's <c>SmtpOptions</c>).
/// </summary>
public sealed class EmailOptions
{
    public const string Section = "Email";

    /// <summary>
    /// The address people open MedApp at, e.g. <c>https://medapp.example.org</c>, which links in emails start with. It's
    /// configured, never taken from a request's Host header, so a forged header can't make a reset link point elsewhere.
    /// Without it, emails that need a link aren't sent.
    /// </summary>
    public string? LinkBaseUrl { get; set; }
}
