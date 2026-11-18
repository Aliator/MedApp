using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Clinics.Repositories;
using MedApp.Application.Common.Exceptions;

namespace MedApp.Application.Clinics.Common;

/// <summary>
/// Whether a clinic session can be at a time: inside the clinician's working day and clear of everything on their
/// calendar (<see cref="CalendarClashes.Find"/>), and in a room that's open and free then
/// (<see cref="ClinicRules.WhyRoomUnavailable"/>). Called holding the clinician's and the room's calendar locks
/// (<see cref="Locks"/>), so two sessions can't take one room, or one person, at once.
/// </summary>
public static class SessionPlacement
{
    /// <summary>The locks a session's change takes: its clinician's and its rooms' (a room's ID keys its lock).</summary>
    public static IReadOnlyCollection<Guid> Locks(Guid staffUserId, params Guid[] roomIds) =>
        new[] { staffUserId }.Concat(roomIds).Distinct().ToList();

    /// <summary>
    /// Why the session can't be there, or null. The session being changed (<paramref name="sessionId"/>) is left out, with
    /// the appointments booked into it. <paramref name="checkPerson"/> false leaves the clinician's calendar out (a session
    /// whose time stays, moving room).
    /// </summary>
    public static async Task<string?> WhyNotAsync(
        ICalendarRepository calendars,
        IClinicRepository clinics,
        Guid staffUserId,
        Guid roomId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct,
        Guid? sessionId = null,
        bool checkPerson = true)
    {
        var start = UkTime.Utc(startUtc);
        var end = UkTime.Utc(endUtc);
        var date = UkTime.DateOf(start);

        if (checkPerson)
        {
            var data = await calendars.GetCalendarDataAsync(staffUserId, date, date, ct);
            if (CalendarClashes.Find(data, start, end, ignoreSessionId: sessionId) is { } clash)
                return clash;
        }

        var day = await clinics.GetRoomDayAsync(roomId, date, ct)
                  ?? throw new NotFoundException($"Room '{roomId}' was not found.");
        return ClinicRules.WhyRoomUnavailable(day, start, end, ignoreSessionId: sessionId);
    }
}
