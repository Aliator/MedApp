using MedApp.Domain.Appointments;

namespace MedApp.Application.Calendar.Common;

/// <summary>Something on a person's calendar that a time could clash with, and what to call it ("Lunch").</summary>
public sealed record CalendarClash(DateTime StartUtc, DateTime EndUtc, string What);

/// <summary>
/// No overlaps: a new or moved appointment, block or meeting may not overlap any appointment (Booked, Attended or Did
/// not attend), block (stored, or from a template) or meeting on that person's calendar (except a meeting they
/// declined, <see cref="MeetingReplies"/>). This finds what a time would overlap, and lists what a calendar is busy
/// with (for <see cref="Availability"/>).
/// </summary>
public static class CalendarClashes
{
    /// <summary>
    /// A message naming the first thing, in time order, that <paramref name="startUtc"/> to <paramref name="endUtc"/>
    /// overlaps, or null if nothing does. It never names a patient. The thing being moved is left out: a stored block
    /// (<paramref name="ignoreBlockId"/>), a template block on that date (<paramref name="ignoreTemplateBlockId"/>), an
    /// appointment (<paramref name="ignoreAppointmentId"/>) or a meeting (<paramref name="ignoreMeetingId"/>).
    /// <para>
    /// The person's working day comes first (<see cref="WhyOutsideWorkingDay"/>: closed, not one of their days, or
    /// outside their hours): the validators refuse those (<see cref="WorkingDays"/>), but before the calendar lock, so a
    /// day closed or hours changed between the validator and the save are caught here, as the handlers call this
    /// holding the lock and <see cref="CalendarData.ClosedDays"/> and <see cref="CalendarData.Hours"/> are read under it.
    /// It also covers commands whose validators can't know the person (moving a block).
    /// </para>
    /// </summary>
    public static string? Find(
        CalendarData data,
        DateTime startUtc,
        DateTime endUtc,
        Guid? ignoreBlockId = null,
        Guid? ignoreTemplateBlockId = null,
        Guid? ignoreAppointmentId = null,
        Guid? ignoreMeetingId = null) =>
        WhyOutsideWorkingDay(data, startUtc, endUtc)
        ?? (FindFirst(data, startUtc, endUtc, ignoreBlockId, ignoreTemplateBlockId, ignoreAppointmentId, ignoreMeetingId) is { } clash
            ? $"That time overlaps {clash.What}, {UkTime.DescribeTime(clash.StartUtc)} to {UkTime.DescribeTime(clash.EndUtc)}."
            : null);

    /// <summary>
    /// Why the time isn't inside the person's working day (as the data has it: closed, not one of their days, or
    /// outside their hours), or null. <paramref name="who"/> names them (a meeting's attendee).
    /// </summary>
    public static string? WhyOutsideWorkingDay(CalendarData data, DateTime startUtc, DateTime endUtc, string? who = null) =>
        data.WorkingTimeOn(UkTime.DateOf(UkTime.Utc(startUtc))).WhyOutside(startUtc, endUtc, who);

    /// <summary>The first thing, in time order, that a time overlaps on the calendar (see <see cref="Find"/>), or null.</summary>
    public static CalendarClash? FindFirst(
        CalendarData data,
        DateTime startUtc,
        DateTime endUtc,
        Guid? ignoreBlockId = null,
        Guid? ignoreTemplateBlockId = null,
        Guid? ignoreAppointmentId = null,
        Guid? ignoreMeetingId = null)
    {
        var start = UkTime.Utc(startUtc);
        var end = UkTime.Utc(endUtc);
        var date = UkTime.DateOf(start);

        return BusyTimes(data, ignoreBlockId, ignoreAppointmentId, ignoreMeetingId, (ignoreTemplateBlockId, date))
            .FirstOrDefault(c => Overlaps(start, end, c.StartUtc, c.EndUtc));
    }

    /// <summary>
    /// Everything that makes the person busy over the data's range, by start then end: appointments (Booked, Attended
    /// and Did not attend), stored blocks, template blocks and meetings, but not a meeting the person declined. A
    /// meeting being moved is left out (<paramref name="ignoreMeetingId"/>).
    /// </summary>
    public static IReadOnlyList<CalendarClash> BusyTimes(CalendarData data, Guid? ignoreMeetingId = null) =>
        BusyTimes(data, null, null, ignoreMeetingId, (null, default));

    /// <summary>
    /// For an appointment recorded after the event: a message naming the first of the person's appointments (Booked,
    /// Attended or Did not attend) that the time overlaps, or null. Blocks and meetings don't count: what happened,
    /// happened, and they show flagged as overlapping it.
    /// </summary>
    public static string? FindAppointment(CalendarData data, DateTime startUtc, DateTime endUtc, Guid? ignoreAppointmentId = null)
    {
        var start = UkTime.Utc(startUtc);
        var end = UkTime.Utc(endUtc);
        var clash = data.Appointments
            .Where(a => a.Id != ignoreAppointmentId
                        && a.Status is AppointmentStatus.Booked or AppointmentStatus.Attended or AppointmentStatus.DidNotAttend
                        && a is { StartUtc: not null, EndUtc: not null }
                        && Overlaps(start, end, a.StartUtc.Value, a.EndUtc.Value))
            .OrderBy(a => a.StartUtc)
            .ThenBy(a => a.EndUtc)
            .FirstOrDefault();

        return clash is null
            ? null
            : $"That time overlaps {AnotherAppointment}, {UkTime.DescribeTime(clash.StartUtc!.Value)} to {UkTime.DescribeTime(clash.EndUtc!.Value)}.";
    }

    /// <summary>What an appointment is called in a clash: never its patient.</summary>
    public const string AnotherAppointment = "another appointment";

    /// <summary>What a meeting is called in a clash or a busy time: "the meeting 'MDT'".</summary>
    public static string Describe(Domain.Calendar.Meeting meeting) => $"the meeting '{meeting.Title}'";

    /// <summary>Whether two times overlap; touching (one ends as the other starts) doesn't.</summary>
    public static bool Overlaps(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd) =>
        aStart < bEnd && bStart < aEnd;

    private static List<CalendarClash> BusyTimes(
        CalendarData data,
        Guid? ignoreBlockId,
        Guid? ignoreAppointmentId,
        Guid? ignoreMeetingId,
        (Guid? Id, DateOnly Date) ignoreTemplateBlock)
    {
        var appointments = data.Appointments
            .Where(a => a.Id != ignoreAppointmentId
                        && a.Status is AppointmentStatus.Booked or AppointmentStatus.Attended or AppointmentStatus.DidNotAttend
                        && a is { StartUtc: not null, EndUtc: not null })
            .Select(a => new CalendarClash(a.StartUtc!.Value, a.EndUtc!.Value, AnotherAppointment));

        var blocks = data.Blocks
            .Where(b => b.Id != ignoreBlockId)
            .Select(b => new CalendarClash(b.StartUtc, b.EndUtc, b.ActivityType?.Name ?? "a block"));

        var templateBlocks = CalendarProjection.TemplateBlocks(data)
            .Where(p => !(p.Source.Id == ignoreTemplateBlock.Id && p.Date == ignoreTemplateBlock.Date))
            .Select(p => new CalendarClash(p.StartUtc, p.EndUtc, p.Source.ActivityType?.Name ?? "a block"));

        // A meeting the person declined isn't busy time for them, though it still is for everyone else going.
        var meetings = data.Meetings
            .Where(m => m.Id != ignoreMeetingId && !MeetingReplies.HasDeclined(m, data.StaffUserId))
            .Select(m => new CalendarClash(m.StartUtc, m.EndUtc, Describe(m)));

        return appointments.Concat(blocks).Concat(templateBlocks).Concat(meetings)
            .OrderBy(c => c.StartUtc)
            .ThenBy(c => c.EndUtc)
            .ToList();
    }
}
