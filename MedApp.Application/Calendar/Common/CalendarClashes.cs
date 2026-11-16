using MedApp.Domain.Appointments;

namespace MedApp.Application.Calendar.Common;

/// <summary>
/// No overlaps: a new or moved appointment or block may not overlap any appointment (Booked, Attended or Did not
/// attend) or block (stored, or from a template) on that person's calendar. This finds what a time would overlap.
/// </summary>
public static class CalendarClashes
{
    /// <summary>
    /// A message naming the first thing, in time order, that <paramref name="startUtc"/> to <paramref name="endUtc"/>
    /// overlaps, or null if nothing does. It never names a patient. The thing being moved is left out: a stored block
    /// (<paramref name="ignoreBlockId"/>), a template block on that date (<paramref name="ignoreTemplateBlockId"/>) or an
    /// appointment (<paramref name="ignoreAppointmentId"/>).
    /// </summary>
    public static string? Find(
        CalendarData data,
        DateTime startUtc,
        DateTime endUtc,
        Guid? ignoreBlockId = null,
        Guid? ignoreTemplateBlockId = null,
        Guid? ignoreAppointmentId = null)
    {
        var start = UkTime.Utc(startUtc);
        var end = UkTime.Utc(endUtc);
        var date = UkTime.DateOf(start);

        var appointments = data.Appointments
            .Where(a => a.Id != ignoreAppointmentId
                        && a.Status is AppointmentStatus.Booked or AppointmentStatus.Attended or AppointmentStatus.DidNotAttend
                        && a is { StartUtc: not null, EndUtc: not null })
            .Select(a => new Clash(a.StartUtc!.Value, a.EndUtc!.Value, "another appointment"));

        var blocks = data.Blocks
            .Where(b => b.Id != ignoreBlockId)
            .Select(b => new Clash(b.StartUtc, b.EndUtc, b.ActivityType?.Name ?? "a block"));

        var templateBlocks = CalendarProjection.TemplateBlocks(data)
            .Where(p => !(p.Source.Id == ignoreTemplateBlockId && p.Date == date))
            .Select(p => new Clash(p.StartUtc, p.EndUtc, p.Source.ActivityType?.Name ?? "a block"));

        var first = appointments.Concat(blocks).Concat(templateBlocks)
            .Where(c => Overlaps(start, end, c.StartUtc, c.EndUtc))
            .OrderBy(c => c.StartUtc)
            .ThenBy(c => c.EndUtc)
            .FirstOrDefault();

        if (first is null)
            return null;

        return $"That time overlaps {first.What}, {UkTime.DescribeTime(first.StartUtc)} to {UkTime.DescribeTime(first.EndUtc)}.";
    }

    /// <summary>Whether two times overlap; touching (one ends as the other starts) doesn't.</summary>
    public static bool Overlaps(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd) =>
        aStart < bEnd && bStart < aEnd;

    private sealed record Clash(DateTime StartUtc, DateTime EndUtc, string What);
}
