namespace MedApp.UnitTests.Common.Fixtures;

/// <summary>A clock that always says the same time, so tests that depend on "now" are repeatable.</summary>
public sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
}
