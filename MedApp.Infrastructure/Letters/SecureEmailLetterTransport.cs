using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MedApp.Application.Letters.Common;
using MedApp.Application.Practices.Common;
using MedApp.Infrastructure.Common.Email;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MedApp.Infrastructure.Letters;

/// <summary>
/// Sends letters by secure email (decision 103), straight to the relay in <c>Letters:SecureEmail</c> with MailKit,
/// <b>synchronously</b>: unlike staff email (the best-effort queue), the caller learns at once whether the server
/// accepted the letter, and a recipient is marked sent only then. Off (<see cref="IsAvailable"/> false) unless the relay
/// is configured and declared to meet DCB1596. Nothing about the patient is in the email's words; the PDF is attached.
/// </summary>
public sealed class SecureEmailLetterTransport(IOptions<LettersOptions> options) : ILetterTransport
{
    public bool IsAvailable => options.Value.SecureEmail.IsAvailable;

    public async Task<string> SendAsync(SecureLetterEmail email, CancellationToken ct)
    {
        var settings = options.Value.SecureEmail;
        if (!settings.IsAvailable)
            throw new InvalidOperationException("Letters:SecureEmail isn't set up, or the relay isn't declared to meet DCB1596.");

        var message = BuildMessage(email, settings);
        using var client = new SmtpClient();
        client.Timeout = (int)TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds)).TotalMilliseconds;
        try
        {
            await client.ConnectAsync(settings.Host!, settings.Port, SmtpEmailTransport.SecurityFor(settings.Security), ct);
            if (!string.IsNullOrEmpty(settings.Username))
                await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(quit: true, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (SmtpCommandException e)
        {
            throw new LetterTransportException($"refused, {(int)e.StatusCode}", e);
        }
        catch (Exception e) when (e is SmtpProtocolException or AuthenticationException or SslHandshakeException
                                      or ServiceNotConnectedException or IOException or System.Net.Sockets.SocketException
                                      or TimeoutException or OperationCanceledException)
        {
            throw new LetterTransportException("connection", e);
        }

        return message.MessageId ?? string.Empty;
    }

    /// <summary>From the centre's secure mailbox, to the one recipient, the words as text and the PDF attached.</summary>
    public static MimeMessage BuildMessage(SecureLetterEmail email, LetterSecureEmailOptions settings)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.CentreName, settings.FromAddress ?? throw new InvalidOperationException("Letters:SecureEmail:FromAddress is not set.")));
        message.To.Add(MailboxAddress.Parse(email.To));
        message.Subject = email.Subject;
        message.MessageId = MimeKit.Utils.MimeUtils.GenerateMessageId();

        var body = new BodyBuilder { TextBody = email.TextBody };
        body.Attachments.Add(email.FileName, email.Pdf, new ContentType("application", "pdf"));
        message.Body = body.ToMessageBody();
        return message;
    }
}
