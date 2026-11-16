using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using MedApp.Application.Common.Email;

namespace MedApp.IntegrationTests.Common;

/// <summary>
/// The API's email sender in the integration tests: it keeps every message instead of sending it (synchronously, so a
/// test can read it as soon as the request returns). Emptied before each test.
/// </summary>
public sealed partial class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public bool CanSend => true;

    public IReadOnlyList<EmailMessage> Sent => _sent.ToList();

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public void Clear() => _sent.Clear();

    /// <summary>The messages to one address.</summary>
    public IReadOnlyList<EmailMessage> To(string address) =>
        Sent.Where(m => string.Equals(m.To, address, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>The account and token in an emailed link's fragment (<c>#user=…&amp;token=…</c>), as the page reads them.</summary>
    public static (Guid UserId, string Token) LinkIn(EmailMessage message)
    {
        var match = LinkPattern().Match(message.TextBody);
        if (!match.Success)
            throw new InvalidOperationException($"No link in the email \"{message.Subject}\".");

        return (Guid.Parse(match.Groups["user"].Value), Uri.UnescapeDataString(match.Groups["token"].Value));
    }

    [GeneratedRegex(@"#user=(?<user>[0-9a-f-]{36})&token=(?<token>[^\s]+)")]
    private static partial Regex LinkPattern();
}
