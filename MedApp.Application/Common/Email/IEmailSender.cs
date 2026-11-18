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

    /// <summary>
    /// Whether an email of <paramref name="category"/> would go out now: a mail server is configured and the hour's
    /// allowance has room for it. A look, not a promise (another request may take the last place), so a handler can say
    /// "we couldn't email you" rather than promise an email that will be dropped.
    /// </summary>
    bool HasRoomFor(EmailCategory category) => CanSend;
}
