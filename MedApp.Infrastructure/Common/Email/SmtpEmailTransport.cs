using MailKit.Net.Smtp;
using MailKit.Security;
using MedApp.Application.Common.Email;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MedApp.Infrastructure.Common.Email;

/// <summary>
/// Sends over SMTP with MailKit (MIT): a connection per message (MedApp sends few), secured as
/// <see cref="SmtpOptions.Security"/> says, signing in when a user name is set.
/// </summary>
public sealed class SmtpEmailTransport(IOptions<EmailDeliveryOptions> options) : IEmailTransport
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var settings = options.Value;
        var smtp = settings.Smtp;

        using var client = new SmtpClient();
        client.Timeout = (int)TimeSpan.FromSeconds(Math.Max(1, smtp.TimeoutSeconds)).TotalMilliseconds;

        var host = smtp.Host ?? throw new InvalidOperationException("Email:Smtp:Host is not set.");
        await client.ConnectAsync(host, smtp.Port, SecurityFor(smtp.Security), ct);
        if (!string.IsNullOrEmpty(smtp.Username))
            await client.AuthenticateAsync(smtp.Username, smtp.Password ?? string.Empty, ct);

        await client.SendAsync(BuildMessage(message, settings), ct);
        await client.DisconnectAsync(quit: true, ct);
    }

    /// <summary>The MIME message: from the configured address, to one person, with the HTML and text bodies as alternatives.</summary>
    public static MimeMessage BuildMessage(EmailMessage message, EmailDeliveryOptions settings)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(
            settings.FromName,
            settings.FromAddress ?? throw new InvalidOperationException("Email:FromAddress is not set.")));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody,
        }.ToMessageBody();
        return mime;
    }

    /// <summary>The MailKit option for a <see cref="SmtpOptions.Security"/> value (ignoring case); anything else requires TLS (StartTls).</summary>
    public static SecureSocketOptions SecurityFor(string? security) =>
        security?.Trim().ToLowerInvariant() switch
        {
            "none" => SecureSocketOptions.None,
            "starttls" => SecureSocketOptions.StartTls,
            "sslonconnect" => SecureSocketOptions.SslOnConnect,
            "auto" => SecureSocketOptions.Auto,
            _ => SecureSocketOptions.StartTls,
        };
}
