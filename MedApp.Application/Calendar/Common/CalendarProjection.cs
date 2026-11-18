using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Common;

/// <summary>
/// One date on a calendar: whether it's worked (one of the person's days, and not closed: a closed day's name it then
/// carries), their working day (<see cref="WorkStartUtc"/> to <see cref="WorkEndUtc"/>), and the day template in force.
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

    /// <summary>It's one of the person's days (their site open), closed or not.</summary>
    public bool HasHours { get; init; }

    /// <summary>Where they work that day, when their hours name a site.</summary>
    public Guid? SiteId { get; init; }

    /// <summary>The start of the grid shown for the day, in UTC: the same clock time on every day of one response.</summary>
    public DateTime GridStartUtc { get; init; }

    /// <summary>The end of the grid shown for the day, in UTC.</summary>
    public DateTime GridEndUtc { get; init; }
}

/// <summary>
/// The clock times a calendar grid shows on every day of one response (a week, or a team's day), so its rows line up:
/// from the earliest working day's start, or anything on the calendars, to the latest end, in whole hours; the default
/// day, 08:00 to 18:00, when nobody works and nothing is on.
/// </summary>
public sealed record GridWindow(TimeOnly Start, TimeOnly End)
{
    public static GridWindow Default { get; } = new(WorkingHours.Start, WorkingHours.End);

    /// <summary>The window that shows everyone's working days and everything on their calendars over their ranges.</summary>
    public static GridWindow Of(IEnumerable<CalendarData> calendars)
    {
        var times = new List<(TimeOnly Start, TimeOnly End)>();
        foreach (var data in calendars)
        {
            for (var date = data.From; date <= data.To; date = date.AddDays(1))
                if (data.WorkingTimeOn(date) is { Starts: { } starts, Ends: { } ends })
                    times.Add((starts, ends));

            var busy = CalendarClashes.BusyTimes(data)
                .Select(c => (c.StartUtc, c.EndUtc))
                .Concat(data.Meetings.Select(m => (m.StartUtc, m.EndUtc)));
            foreach (var (startUtc, endUtc) in busy)
            {
                var date = UkTime.DateOf(startUtc);
                if (date < data.From || date > data.To)
                    continue;
                // Nothing runs past its day (the working day rule), but if it did, it shows to the latest time.
                times.Add((UkTime.TimeOf(startUtc), UkTime.DateOf(endUtc) > date ? WorkingHours.Latest : UkTime.TimeOf(endUtc)));
            }
        }

        if (times.Count == 0)
            return Default;

        var first = times.Min(t => t.Start);
        var last = times.Max(t => t.End);
        var endHour = Math.Min(last.Minute > 0 || last.Second > 0 ? last.Hour + 1 : last.Hour, 23);
        var window = new GridWindow(new TimeOnly(first.Hour, 0), new TimeOnly(endHour, 0));
        return window.End > window.Start ? window : Default;
    }
}

/// <summary>A day template block as it falls on one date of a calendar, in UTC.</summary>
public sealed record ProjectedBlock(DayTemplateBlock Source, DateOnly Date, DateTime StartUtc, DateTime EndUtc);

/// <summary>
/// Templates are applied when a calendar is read, never generated: this works out each date's day template and its
/// blocks from the rows <see cref="CalendarData"/> holds.
/// <para>
/// On a day the person works, a day override for the date wins (its day template, or none). Otherwise the person's default week
/// template valid on the date gives the day template for that weekday, if its row for that weekday is valid on the
/// date. That day template's blocks valid on the date are projected, except those with an exception for the date.
/// Days they don't work and closed days (everywhere, or at their site) have nothing (an override on a day that later closed is kept, but doesn't apply while it's
/// closed). Because every row is dated, a past day projects exactly what applied then.
/// </para>
/// </summary>
public static class CalendarProjection
{
    /// <summary>
    /// Each date in the data's range, in order, with the person's working day on each, and the grid shown for them
    /// (<paramref name="window"/>: by default, the one that fits this calendar).
    /// </summary>
    public static IReadOnlyList<CalendarDay> Days(CalendarData data, DateOnly today, GridWindow? window = null)
    {
        window ??= GridWindow.Of([data]);
        var days = new List<CalendarDay>();
        for (var date = data.From; date <= data.To; date = date.AddDays(1))
        {
            var (dayTemplateId, overridden) = DayTemplateOn(data, date);
            var working = data.WorkingTimeOn(date);
            days.Add(new CalendarDay(
                date,
                working.IsWorking,
                date < today,
                date == today,
                working.StartUtc,
                working.EndUtc,
                dayTemplateId,
                dayTemplateId is { } id ? data.DayTemplateNames.GetValueOrDefault(id) : null,
                overridden)
            {
                ClosedDayName = working.ClosedDayName,
                HasHours = working.HasHours,
                SiteId = working.SiteId,
                GridStartUtc = UkTime.ToUtc(date, window.Start),
                GridEndUtc = UkTime.ToUtc(date, window.End)
            });
        }

        return days;
    }

    /// <summary>
    /// A team's day (the team view's "Everyone free" column): worked when everyone shown works it, from the latest of
    /// their starts to the earliest of their ends (as <see cref="Availability"/> sees it); with nobody shown, the
    /// default day. Its closed day is the one closed everywhere.
    /// </summary>
    public static CalendarDay TeamDay(
        IReadOnlyCollection<CalendarData> calendars, DateOnly date, DateOnly today, ClosedDaySet closedDays, GridWindow window)
    {
        List<WorkingTime> everyone = calendars.Count == 0
            ? [WorkingTimeSet.None.For(Guid.Empty, date, closedDays)]
            : calendars.Select(c => c.WorkingTimeOn(date)).ToList();
        var hasHours = everyone.All(w => w.HasHours);
        var start = hasHours ? everyone.Max(w => w.StartUtc) : WorkingHours.DayStartUtc(date);
        var end = hasHours ? everyone.Min(w => w.EndUtc) : WorkingHours.DayEndUtc(date);
        return new CalendarDay(
            date, everyone.All(w => w.IsWorking) && closedDays.NameOn(date) is null && start < end, date < today, date == today, start, end, null, null, false)
        {
            ClosedDayName = closedDays.NameOn(date),
            HasHours = hasHours && start < end,
            GridStartUtc = UkTime.ToUtc(date, window.Start),
            GridEndUtc = UkTime.ToUtc(date, window.End)
        };
    }

    /// <summary>The day template blocks on each working day of the data's range, by date then start.</summary>
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
    /// The day template in force on a date (null for none, and on days not worked and closed days), and whether it's the
    /// day's own.
    /// </summary>
    public static (Guid? DayTemplateId, bool IsOverridden) DayTemplateOn(CalendarData data, DateOnly date)
    {
        if (!data.WorkingTimeOn(date).IsWorking)
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
