using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Repositories;

/// <summary>
/// Meetings. Changes that put a meeting on calendars run inside <see cref="ICalendarLock"/>, holding every attendee's
/// lock, which these share. Calendars read their meetings through <see cref="ICalendarRepository"/>.
/// </summary>
public interface IMeetingRepository
{
    /// <summary>A meeting with its attendees, read-only.</summary>
    Task<Meeting?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>A meeting with its attendees, tracked so <see cref="SaveAsync"/> saves changes to it.</summary>
    Task<Meeting?> GetForUpdateAsync(Guid id, CancellationToken ct);

    Task AddAsync(Meeting meeting, CancellationToken ct);

    /// <summary>
    /// Saves changes to a meeting loaded for update. If someone else saved it since it was loaded (its row version), a
    /// 409 (<see cref="Calendar.Common.MeetingLimits.ChangedMeanwhile"/>), and nothing is saved.
    /// </summary>
    Task SaveAsync(CancellationToken ct);

    /// <summary>Deletes a meeting loaded for update, with its attendees.</summary>
    Task DeleteAsync(Meeting meeting, CancellationToken ct);

    /// <summary>
    /// Takes someone off every meeting that hasn't started by <paramref name="nowUtc"/> (their account is going, or
    /// stops being staff), and deletes those left with nobody. Past meetings keep them. Returns how many they were taken
    /// off.
    /// </summary>
    Task<int> RemoveAttendeeFromUpcomingAsync(Guid staffUserId, DateTime nowUtc, CancellationToken ct);
}
