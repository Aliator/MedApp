using System.Threading.Channels;
using MedApp.Application.Common.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedApp.Infrastructure.Common.Email;

/// <summary>
/// The app's <see cref="IEmailSender"/>: a queue in memory, which <see cref="EmailDispatcher"/> sends from in the
/// background. So sending never holds a request up or fails it, and a request whose email goes out takes no longer than
/// one whose doesn't (a password reset request can't be timed to learn whether an account exists). It's best effort: a
/// full queue drops the message, and messages still waiting when the app stops are lost; both are logged, never with
/// the address.
/// </summary>
public sealed class EmailQueue : IEmailSender
{
    private readonly Channel<EmailMessage> _channel;
    private readonly EmailDeliveryOptions _options;
    private readonly ILogger<EmailQueue> _logger;

    public EmailQueue(IOptions<EmailDeliveryOptions> options, ILogger<EmailQueue> logger)
    {
        _options = options.Value;
        _logger = logger;
        _channel = Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(Math.Max(1, _options.QueueCapacity))
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait,
        });
    }

    public bool CanSend => _options.CanSend;

    /// <summary>The messages waiting, for the dispatcher.</summary>
    public ChannelReader<EmailMessage> Reader => _channel.Reader;

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (!CanSend)
        {
            _logger.LogDebug("Email not sent, as no mail server is configured: {Subject}", message.Subject);
            return Task.CompletedTask;
        }

        if (!_channel.Writer.TryWrite(message))
            _logger.LogWarning("Email dropped, as {Capacity} are already waiting to be sent: {Subject}", _options.QueueCapacity, message.Subject);

        return Task.CompletedTask;
    }
}
