using System.Threading.Channels;
using MedApp.Application.Common.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedApp.Infrastructure.Common.Email;

/// <summary>
/// The app's <see cref="IEmailSender"/>: two queues in memory, which <see cref="EmailDispatcher"/> sends from in the
/// background: notices and two-factor codes (someone is waiting for each), then links. So sending never holds a request up or fails it, and a request whose
/// email goes out takes no longer than one whose doesn't (a password reset request can't be timed to learn whether an
/// account exists). It's best effort: a full queue drops the message, and messages still waiting when the app stops are
/// lost; both are logged by category only, never with the address or subject.
/// </summary>
public sealed class EmailQueue : IEmailSender
{
    private readonly Channel<EmailMessage> _notices;
    private readonly Channel<EmailMessage> _links;
    private readonly EmailDeliveryOptions _options;
    private readonly ILogger<EmailQueue> _logger;

    public EmailQueue(IOptions<EmailDeliveryOptions> options, ILogger<EmailQueue> logger, TimeProvider? timeProvider = null)
    {
        _options = options.Value;
        _logger = logger;
        _notices = Create(_options.QueueCapacity);
        _links = Create(_options.QueueCapacity);
        Budget = new EmailBudget(_options.MaxPerHour, timeProvider ?? TimeProvider.System);
    }

    public bool CanSend => _options.CanSend;

    /// <summary>This copy's hourly allowance, which <see cref="EmailDispatcher"/> takes from as it sends.</summary>
    public EmailBudget Budget { get; }

    public bool HasRoomFor(EmailCategory category) => CanSend && Budget.HasRoom(category);

    /// <summary>Notices and two-factor codes waiting, sent before any link.</summary>
    public ChannelReader<EmailMessage> Notices => _notices.Reader;

    /// <summary>Links waiting.</summary>
    public ChannelReader<EmailMessage> Links => _links.Reader;

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var category = message.Category;
        if (!CanSend)
        {
            _logger.LogDebug("Email not sent, as no mail server is configured ({EmailCategory}).", category);
            return Task.CompletedTask;
        }

        // Codes are as urgent as notices (someone is waiting to sign in), but keep their own category for the hourly limit.
        var queue = category == EmailCategory.Link ? _links : _notices;
        if (!queue.Writer.TryWrite(message))
            _logger.LogWarning("Email dropped ({EmailCategory}), as {Capacity} are already waiting to be sent.", category, _options.QueueCapacity);

        return Task.CompletedTask;
    }

    /// <summary>The next message, a notice before any link; null if none is waiting.</summary>
    public EmailMessage? TryTakeNext() =>
        _notices.Reader.TryRead(out var notice) ? notice
        : _links.Reader.TryRead(out var link) ? link
        : null;

    /// <summary>Waits until a message is waiting in either queue.</summary>
    public async Task WaitAsync(CancellationToken ct)
    {
        if (_notices.Reader.TryPeek(out _) || _links.Reader.TryPeek(out _))
            return;

        await Task.WhenAny(
            _notices.Reader.WaitToReadAsync(ct).AsTask(),
            _links.Reader.WaitToReadAsync(ct).AsTask());
        ct.ThrowIfCancellationRequested();
    }

    private static Channel<EmailMessage> Create(int capacity) =>
        Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(Math.Max(1, capacity))
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait,
        });
}
