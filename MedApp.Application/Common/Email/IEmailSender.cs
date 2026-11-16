namespace MedApp.Application.Common.Email;

/// <summary>
/// Sends email, best effort: it never throws for a failure to send (that's logged), and returns quickly, so a handler
/// can send after its own change is saved without the email holding the request up or undoing the change. With no mail
/// server configured (<see cref="CanSend"/> false), nothing is sent.
/// </summary>
public interface IEmailSender
{
    /// <summary>Whether a mail server is configured, so a message would be sent.</summary>
    bool CanSend { get; }

    Task SendAsync(EmailMessage message, CancellationToken ct);
}
