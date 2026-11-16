using MedApp.Application.Calendar.Repositories;

namespace MedApp.UnitTests.Common.Fixtures;

/// <summary>
/// A calendar lock for handler tests: runs the work straight away, and records which members of staff each call
/// locked and the token it was given, so a test can check a handler took the lock it should.
/// </summary>
public sealed class RecordingCalendarLock : ICalendarLock
{
    /// <summary>The staff IDs passed to each call, in order.</summary>
    public List<IReadOnlyCollection<Guid>> Locked { get; } = [];

    /// <summary>The cancellation token passed to each call, in order.</summary>
    public List<CancellationToken> Tokens { get; } = [];

    public async Task<T> RunAsync<T>(IReadOnlyCollection<Guid> staffUserIds, Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        Locked.Add(staffUserIds);
        Tokens.Add(ct);
        return await work(ct);
    }

    /// <summary>The staff IDs of the one call made.</summary>
    public IReadOnlyCollection<Guid> LockedOnce => Locked.Single();
}
