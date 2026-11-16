namespace MedApp.Application.Calendar.Repositories;

/// <summary>
/// Serialises changes to members of staff's calendars, so two requests racing for the same slot can't both pass the
/// clash check: one waits for the other, then sees what it saved and gets a 409.
/// <para>
/// The work runs in one database transaction holding a lock per member of staff, and must load what it checks and
/// save its change inside the delegate: the repositories share the transaction. It may run more than once (the
/// database retries transient failures as a whole), so it mustn't use entities loaded before it. Notifications are
/// sent after it returns.
/// </para>
/// </summary>
public interface ICalendarLock
{
    /// <summary>
    /// Runs <paramref name="work"/> holding the calendar locks of these members of staff, committing what it saves.
    /// If a lock isn't granted in time, it throws a <c>ConflictException</c> ("The calendar is busy; please try again.").
    /// </summary>
    Task<T> RunAsync<T>(IReadOnlyCollection<Guid> staffUserIds, Func<CancellationToken, Task<T>> work, CancellationToken ct);
}
