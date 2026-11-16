namespace MedApp.IntegrationTests.Common;

/// <summary>
/// The API's clock in the integration tests: the real time plus an offset a test can move, so a test can let time
/// pass (an appointment's start, a day becoming past) without waiting. It starts with no offset and is put back before
/// every test. Sessions read the real clock, not this one, so moving it doesn't expire them.
/// </summary>
public sealed class TestClock : TimeProvider
{
    private long _offsetTicks;

    public TimeSpan Offset => TimeSpan.FromTicks(Interlocked.Read(ref _offsetTicks));

    public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + Offset;

    /// <summary>Moves the clock on (or back) by this much.</summary>
    public void Advance(TimeSpan by) => Interlocked.Add(ref _offsetTicks, by.Ticks);

    /// <summary>Sets the clock to this UTC moment (and lets it run on from there).</summary>
    public void SetUtcNow(DateTime utcNow) =>
        Interlocked.Exchange(ref _offsetTicks, (DateTime.SpecifyKind(utcNow, DateTimeKind.Utc) - base.GetUtcNow().UtcDateTime).Ticks);

    /// <summary>Back to the real time.</summary>
    public void Reset() => Interlocked.Exchange(ref _offsetTicks, 0);
}
