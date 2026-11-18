using MedApp.Application.Letters.Common;

namespace MedApp.IntegrationTests.Common;

/// <summary>
/// The secure email relay for letters, kept in memory: set up and declared, so the API offers secure email, keeping
/// every letter it's given. <see cref="FailNext"/> makes the next one fail as a relay that refuses it would.
/// </summary>
public sealed class FakeLetterTransport : ILetterTransport
{
    private readonly List<SecureLetterEmail> _sent = [];

    public bool IsAvailable => true;

    /// <summary>Make the next letter fail (the relay refuses it).</summary>
    public bool FailNext { get; set; }

    public IReadOnlyList<SecureLetterEmail> Sent
    {
        get
        {
            lock (_sent)
                return _sent.ToList();
        }
    }

    public Task<string> SendAsync(SecureLetterEmail email, CancellationToken ct)
    {
        if (FailNext)
        {
            FailNext = false;
            throw new LetterTransportException("refused, 550");
        }

        lock (_sent)
            _sent.Add(email);
        return Task.FromResult($"<{Guid.NewGuid():N}@relay.invalid>");
    }

    public void Clear()
    {
        FailNext = false;
        lock (_sent)
            _sent.Clear();
    }
}
