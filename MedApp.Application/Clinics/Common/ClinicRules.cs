using MedApp.Application.Appointments.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Clinics;
using MedApp.Domain.Sites;

namespace MedApp.Application.Clinics.Common;

/// <summary>
/// The rules for clinic sessions (centre plan decisions 123 to 127): their slots, and whether a room is free. A room
/// holds one session at a time, and an appointment outside a session that names a room clashes with anything else in
/// it; a site's closed day closes its rooms, and its opening hours bound them. Messages never name a patient.
/// </summary>
public static class ClinicRules
{
    public const string ChangedMeanwhile =
        "Someone changed this clinic session while you had it open. Reload to see the change, then try again.";

    public const string PatternChangedMeanwhile =
        "Someone changed this clinic pattern while you had it open. Reload to see the change, then try again.";

    public const string SessionCancelled = "This clinic session has been cancelled, so nothing can be booked into it.";

    public const string AlreadyCancelled = "This clinic session has already been cancelled.";

    public const string NotSessionsClinician = "A clinic session's slots are booked with its own clinician.";

    public const string NotSessionsRoom = "An appointment in a clinic session is in the session's room.";

    public const string HasBookings =
        "This clinic session has appointments booked into it, so only its room and specialty can change. Cancel it and make another to change its time or slots.";

    public const string SlotTaken = "That slot has just been booked. Choose another.";

    public const string InThePast = "Clinic sessions can't be added in the past.";

    public static readonly string SlotLength =
        $"A slot is {ClinicLimits.MinSlotMinutes} to {ClinicLimits.MaxSlotMinutes} minutes long, in steps of 5.";

    public const string SlotsDontDivide = "The session must divide into whole slots: its length must be a multiple of the slot length.";

    public static string NotASlot(ClinicSession session) =>
        $"Choose one of the session's {session.SlotMinutes}-minute slots, from {UkTime.DescribeTime(session.StartUtc)}.";

    public static string RoomArchived(Room room) => $"{room.Name} is archived, so it can't be chosen for anything new.";

    /// <summary>Why a session can't have these times and slot length (their shape only), or null.</summary>
    public static string? WhyBadSlots(DateTime startUtc, DateTime endUtc, int slotMinutes)
    {
        if (slotMinutes < ClinicLimits.MinSlotMinutes || slotMinutes > ClinicLimits.MaxSlotMinutes || slotMinutes % 5 != 0)
            return SlotLength;

        var minutes = (UkTime.Utc(endUtc) - UkTime.Utc(startUtc)).TotalMinutes;
        return minutes > 0 && minutes % slotMinutes != 0 ? SlotsDontDivide : null;
    }

    /// <summary>A session's slots, in order: each <see cref="ClinicSession.SlotMinutes"/> long, from its start to its end.</summary>
    public static IReadOnlyList<(DateTime StartUtc, DateTime EndUtc)> Slots(ClinicSession session)
    {
        var slots = new List<(DateTime, DateTime)>();
        if (session.SlotMinutes <= 0)
            return slots;

        for (var start = UkTime.Utc(session.StartUtc);
             start.AddMinutes(session.SlotMinutes) <= UkTime.Utc(session.EndUtc);
             start = start.AddMinutes(session.SlotMinutes))
            slots.Add((start, start.AddMinutes(session.SlotMinutes)));
        return slots;
    }

    /// <summary>Whether these times are exactly one of the session's slots.</summary>
    public static bool IsSlot(ClinicSession session, DateTime startUtc, DateTime endUtc)
    {
        var start = UkTime.Utc(startUtc);
        var end = UkTime.Utc(endUtc);
        return Slots(session).Any(s => s.StartUtc == start && s.EndUtc == end);
    }

    /// <summary>
    /// How many of a session's slots are booked (an appointment on the calendar starts in it) and how many are free and
    /// haven't started by <paramref name="nowUtc"/>.
    /// </summary>
    public static (int Booked, int Free) Counts(ClinicSession session, IEnumerable<DateTime> bookedStarts, DateTime nowUtc)
    {
        var booked = bookedStarts.Select(UkTime.Utc).ToHashSet();
        return (Slots(session).Count(s => booked.Contains(s.StartUtc)), FreeStarts(session, booked, nowUtc).Count);
    }

    /// <summary>The starts of a session's slots with no appointment that haven't started by <paramref name="nowUtc"/>.</summary>
    public static IReadOnlyList<DateTime> FreeStarts(ClinicSession session, IEnumerable<DateTime> bookedStarts, DateTime nowUtc)
    {
        var booked = bookedStarts.Select(UkTime.Utc).ToHashSet();
        return Slots(session).Where(s => !booked.Contains(s.StartUtc) && s.StartUtc > nowUtc).Select(s => s.StartUtc).ToList();
    }

    /// <summary>
    /// Why the room can't be used from <paramref name="startUtc"/> to <paramref name="endUtc"/> on its day, or null if
    /// it can: its site is closed that date (everywhere, or the site's own closed day), isn't open that weekday or then,
    /// or another session or appointment is in the room then. The thing being changed is left out: a session
    /// (<paramref name="ignoreSessionId"/>, with the appointments booked into it) or an appointment
    /// (<paramref name="ignoreAppointmentId"/>).
    /// </summary>
    public static string? WhyRoomUnavailable(
        RoomDay day, DateTime startUtc, DateTime endUtc, Guid? ignoreSessionId = null, Guid? ignoreAppointmentId = null)
    {
        var room = day.Room;
        var siteName = SiteName(room);
        if (WhyClosed(day) is { } closed)
            return closed;

        var start = UkTime.Utc(startUtc);
        var end = UkTime.Utc(endUtc);
        var opening = WorkingTimeSet.Opening(day.SiteHours, day.Date.DayOfWeek);
        if (opening is null)
            return $"{siteName} isn't open on {day.Date.DayOfWeek}s, so {room.Name} can't be used.";

        if (UkTime.TimeOf(start) < opening.Value.Opens || UkTime.TimeOf(end) > opening.Value.Closes || UkTime.DateOf(start) != day.Date)
            return $"{siteName} is open {WorkingHours.Describe(opening.Value.Opens)} to {WorkingHours.Describe(opening.Value.Closes)} on {day.Date.DayOfWeek}s.";

        var taken = day.Sessions
            .Where(s => s.Id != ignoreSessionId && s.Status == ClinicSessionStatus.Planned)
            .Select(s => (s.StartUtc, s.EndUtc, What: "a clinic session"))
            .Concat(day.Appointments
                .Where(a => a.Id != ignoreAppointmentId
                            && (ignoreSessionId is null || a.ClinicSessionId != ignoreSessionId)
                            && AppointmentWorkflow.IsOnCalendar(a.Status)
                            && a is { StartUtc: not null, EndUtc: not null })
                .Select(a => (StartUtc: a.StartUtc!.Value, EndUtc: a.EndUtc!.Value, What: CalendarClashes.AnotherAppointment)))
            .Where(t => CalendarClashes.Overlaps(start, end, UkTime.Utc(t.StartUtc), UkTime.Utc(t.EndUtc)))
            .OrderBy(t => t.StartUtc)
            .FirstOrDefault();

        return taken.What is null
            ? null
            : $"{room.Name} is taken by {taken.What}, {UkTime.DescribeTime(taken.StartUtc)} to {UkTime.DescribeTime(taken.EndUtc)}.";
    }

    /// <summary>
    /// Why the room's site is closed on its day (a day closed everywhere, or the site's own closed day, centre plan
    /// decision 121), or null if it's open. Nothing new can be booked in the room then, in a session or not.
    /// </summary>
    public static string? WhyClosed(RoomDay day) =>
        day.ClosedDays.NameAt(day.Date, day.Room.SiteId) is { } closed
            ? $"{SiteName(day.Room)} is closed on {UkTime.DescribeDate(day.Date)} ({closed}), so {day.Room.Name} can't be used."
            : null;

    private static string SiteName(Room room) => room.Site?.Name ?? "Its site";

    /// <summary>A 409 when the session isn't as the caller last read it.</summary>
    public static void EnsureUnchanged(ClinicSession session, DateTime expectedLastUpdated)
    {
        if (session.LastUpdated != UkTime.Utc(expectedLastUpdated))
            throw new ConflictException(ChangedMeanwhile);
    }

    /// <summary>A 409 unless the session is Planned.</summary>
    public static void EnsurePlanned(ClinicSession session)
    {
        if (session.Status != ClinicSessionStatus.Planned)
            throw new ConflictException(AlreadyCancelled);
    }
}
