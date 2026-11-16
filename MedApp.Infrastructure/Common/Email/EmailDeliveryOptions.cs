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

    /// <summary>How many emails can wait to be sent, in each queue (notices, links), before more are dropped (and logged).</summary>
    public int QueueCapacity { get; set; } = 1000;

    /// <summary>
    /// The most emails sent in a clock hour, by each copy of the API (<see cref="EmailBudget"/>): keep it under the mail
    /// provider's quota (Azure's managed domain allows 10 an hour for the whole subscription, a custom domain 100).
    /// </summary>
    public int MaxPerHour { get; set; } = 100;

    /// <summary>Whether email can be sent: a server and a from address.</summary>
    public bool CanSend => !string.IsNullOrWhiteSpace(Smtp.Host) && !string.IsNullOrWhiteSpace(FromAddress);
}

/// <summary>The mail server (<c>Email:Smtp</c>).</summary>
public sealed class SmtpOptions
{
    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    /// <summary>
    /// How the connection is secured: <c>StartTls</c> (the default: TLS is required, so nobody on the way can strip it and
    /// read the password and the links), <c>SslOnConnect</c>, <c>Auto</c> (TLS only if the server offers it), or
    /// <c>None</c> (only for a local catcher such as Mailpit).
    /// </summary>
    public string Security { get; set; } = "StartTls";

    /// <summary>The user name to sign in with; none means the server doesn't ask.</summary>
    public string? Username { get; set; }

    public string? Password { get; set; }

    public int TimeoutSeconds { get; set; } = 30;
}
