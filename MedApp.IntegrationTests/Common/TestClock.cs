namespace MedApp.IntegrationTests.Common;

/// <summary>
/// The API's clock in the integration tests: the real time plus an offset a test can move forward, so it can make a
/// sign-in challenge expire or reach the next authenticator code without waiting. Reset to the real time before each
/// test. Only what reads <see cref="TimeProvider"/> sees it (sessions use the real clock).
/// </summary>
public sealed class TestClock : TimeProvider
{
    private long _offsetTicks;

    public TimeSpan Offset => TimeSpan.FromTicks(Interlocked.Read(ref _offsetTicks));

    public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + Offset;

    public void Advance(TimeSpan by) => Interlocked.Add(ref _offsetTicks, by.Ticks);

    public void Reset() => Interlocked.Exchange(ref _offsetTicks, 0);
}
