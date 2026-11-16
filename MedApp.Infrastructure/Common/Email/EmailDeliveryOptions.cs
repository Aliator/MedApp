namespace MedApp.Infrastructure.Common.Email;

/// <summary>
/// How email is sent: the "Email" configuration section (beside <c>Email:LinkBaseUrl</c>, which the Application reads).
/// Nothing is sent unless <see cref="SmtpOptions.Host"/> and <see cref="FromAddress"/> are both set.
/// <list type="bullet">
/// <item>Development: the AppHost's Mailpit, which catches everything (no TLS, no sign-in).</item>
/// <item>kind: the chart's Mailpit, behind <c>mailpit.enabled</c>.</item>
/// <item>Azure: Azure Communication Services' SMTP relay (<c>smtp.azurecomm.net</c>, port 587, STARTTLS), its user name
/// and password from Key Vault.</item>
/// </list>
/// </summary>
public sealed class EmailDeliveryOptions
{
    public const string Section = "Email";

    /// <summary>The address emails come from, e.g. <c>DoNotReply@medapp.example.org</c>.</summary>
    public string? FromAddress { get; set; }

    /// <summary>The name shown beside it.</summary>
    public string FromName { get; set; } = "MedApp";

    public SmtpOptions Smtp { get; set; } = new();

    /// <summary>How many emails can wait to be sent before more are dropped (and logged).</summary>
    public int QueueCapacity { get; set; } = 1000;

    /// <summary>Whether email can be sent: a server and a from address.</summary>
    public bool CanSend => !string.IsNullOrWhiteSpace(Smtp.Host) && !string.IsNullOrWhiteSpace(FromAddress);
}

/// <summary>The mail server (<c>Email:Smtp</c>).</summary>
public sealed class SmtpOptions
{
    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    /// <summary>
    /// How the connection is secured: <c>Auto</c> (MailKit decides from the port), <c>StartTls</c>, <c>SslOnConnect</c>,
    /// or <c>None</c> (only for a local catcher such as Mailpit).
    /// </summary>
    public string Security { get; set; } = "Auto";

    /// <summary>The user name to sign in with; none means the server doesn't ask.</summary>
    public string? Username { get; set; }

    public string? Password { get; set; }

    public int TimeoutSeconds { get; set; } = 30;
}
