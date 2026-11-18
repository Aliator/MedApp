using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Common;

/// <summary>
/// One date on a calendar: whether it's worked (not a weekend or a closed day, whose name it then carries), and the
/// day template in force.
/// </summary>
public sealed record CalendarDay(
    DateOnly Date,
    bool IsWorkingDay,
    bool IsPast,
    bool IsToday,
    DateTime WorkStartUtc,
    DateTime WorkEndUtc,
    Guid? DayTemplateId,
    string? DayTemplateName,
    bool IsDayTemplateOverridden)
{
    /// <summary>The closed day's name when the clinic is closed that day ("Christmas Day"); null otherwise.</summary>
    public string? ClosedDayName { get; init; }
}

/// <summary>A day template block as it falls on one date of a calendar, in UTC.</summary>
public sealed record ProjectedBlock(DayTemplateBlock Source, DateOnly Date, DateTime StartUtc, DateTime EndUtc);

/// <summary>
/// Templates are applied when a calendar is read, never generated: this works out each date's day template and its
/// blocks from the rows <see cref="CalendarData"/> holds.
/// <para>
/// On a weekday, a day override for the date wins (its day template, or none). Otherwise the person's default week
/// template valid on the date gives the day template for that weekday, if its row for that weekday is valid on the
/// date. That day template's blocks valid on the date are projected, except those with an exception for the date.
/// Weekends and closed days have nothing (an override on a day that later closed is kept, but doesn't apply while it's
/// closed). Because every row is dated, a past day projects exactly what applied then.
/// </para>
/// </summary>
public static class CalendarProjection
{
    /// <summary>Each date in the data's range, in order.</summary>
    public static IReadOnlyList<CalendarDay> Days(CalendarData data, DateOnly today)
    {
        var days = new List<CalendarDay>();
        for (var date = data.From; date <= data.To; date = date.AddDays(1))
        {
            var (dayTemplateId, overridden) = DayTemplateOn(data, date);
            days.Add(new CalendarDay(
                date,
                WorkingHours.IsWorkingDay(date, data.ClosedDays),
                date < today,
                date == today,
                WorkingHours.DayStartUtc(date),
                WorkingHours.DayEndUtc(date),
                dayTemplateId,
                dayTemplateId is { } id ? data.DayTemplateNames.GetValueOrDefault(id) : null,
                overridden)
            {
                ClosedDayName = data.ClosedDays.NameOn(date)
            });
        }

        return days;
    }

    /// <summary>The day template blocks on each weekday of the data's range, by date then start.</summary>
    public static IReadOnlyList<ProjectedBlock> TemplateBlocks(CalendarData data)
    {
        var blocks = new List<ProjectedBlock>();
        for (var date = data.From; date <= data.To; date = date.AddDays(1))
        {
            if (DayTemplateOn(data, date).DayTemplateId is not { } dayTemplateId)
                continue;

            var day = date;
            blocks.AddRange(data.DayTemplateBlocks
                .Where(b => b.DayTemplateId == dayTemplateId && b.IsValidOn(day))
                .Where(b => !data.BlockExceptions.Any(e =>
                    e.StaffUserId == data.StaffUserId && e.Date == day && e.DayTemplateBlockId == b.Id))
                .OrderBy(b => b.StartTime)
                .ThenBy(b => b.Id)
                .Select(b => new ProjectedBlock(b, day, UkTime.ToUtc(day, b.StartTime), UkTime.ToUtc(day, b.EndTime))));
        }

        return blocks;
    }

    /// <summary>
    /// The day template in force on a date (null for none, and on weekends and closed days), and whether it's the
    /// day's own.
    /// </summary>
    public static (Guid? DayTemplateId, bool IsOverridden) DayTemplateOn(CalendarData data, DateOnly date)
    {
        if (!WorkingHours.IsWorkingDay(date, data.ClosedDays))
            return (null, false);

        var dayOverride = data.DayOverrides.FirstOrDefault(o => o.StaffUserId == data.StaffUserId && o.Date == date);
        if (dayOverride is not null)
            return (dayOverride.DayTemplateId, true);

        if (DefaultWeekTemplateOn(data, date) is not { } week)
            return (null, false);

        var day = data.WeekTemplateDays
            .Where(d => d.WeekTemplateId == week.WeekTemplateId && d.DayOfWeek == date.DayOfWeek && d.IsValidOn(date))
            .MaxBy(d => d.ValidFrom);
        return (day?.DayTemplateId, false);
    }

    /// <summary>The person's default week template row valid on a date, if any.</summary>
    public static StaffWeekTemplate? DefaultWeekTemplateOn(CalendarData data, DateOnly date) =>
        data.StaffWeekTemplates
            .Where(s => s.StaffUserId == data.StaffUserId && s.IsValidOn(date))
            .MaxBy(s => s.ValidFrom);
}
