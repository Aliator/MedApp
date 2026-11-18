using MedApp.Domain.Calendar;
using MedApp.Domain.Sites;

namespace MedApp.Application.Calendar.Common;

/// <summary>
/// One person's working day on one UK date (centre plan decision 120): when they'd work (<see cref="Starts"/> to
/// <see cref="Ends"/>, null when they don't work that day), where, and the closed day that stops them, if any. Built by
/// <see cref="WorkingTimeSet.For"/> from their hours, their site's hours and the closed days.
/// </summary>
/// <param name="HasOwnHours">They have hours of their own on the date (not the default).</param>
public sealed record WorkingTime(
    DateOnly Date,
    TimeOnly? Starts,
    TimeOnly? Ends,
    Guid? SiteId,
    string? ClosedDayName,
    bool HasOwnHours)
{
    /// <summary>They'd work this date (it's one of their days, and their site is open), closed or not.</summary>
    public bool HasHours => Starts is not null && Ends is not null;

    /// <summary>They work this date: one of their days, and not closed (everywhere, or at their site).</summary>
    public bool IsWorking => HasHours && ClosedDayName is null;

    /// <summary>Their day's start in UTC, or the default's on a day they don't work (for showing the day).</summary>
    public DateTime StartUtc => UkTime.ToUtc(Date, Starts ?? WorkingHours.Start);

    /// <summary>Their day's end in UTC, or the default's on a day they don't work.</summary>
    public DateTime EndUtc => UkTime.ToUtc(Date, Ends ?? WorkingHours.End);

    /// <summary>
    /// Why nothing new can go on their calendar this date (closed, or not one of their days), or null if it can.
    /// <paramref name="who"/> names them (a meeting's attendee); otherwise "This member of staff".
    /// </summary>
    public string? WhyNotWorking(string? who = null, bool closedDaysCount = true) =>
        closedDaysCount && ClosedDayName is { } name ? WorkingHours.ClosedDay(Date, name)
        : !HasHours ? WorkingHours.NotWorking(Date.DayOfWeek, who)
        : null;

    /// <summary>
    /// Why a time on this date isn't inside their working day, or null if it is. The time's own shape (one day, 5-minute
    /// boundaries) is <see cref="WorkingHours.WhyBadShape"/>'s. A closed day counts unless <paramref name="closedDaysCount"/>
    /// is false (a walk-in, decision 74).
    /// </summary>
    public string? WhyOutside(DateTime startUtc, DateTime endUtc, string? who = null, bool closedDaysCount = true)
    {
        if (WhyNotWorking(who, closedDaysCount) is { } why)
            return why;

        var start = UkTime.TimeOf(UkTime.Utc(startUtc));
        var end = UkTime.TimeOf(UkTime.Utc(endUtc));
        if (start >= Starts && end <= Ends && UkTime.DateOf(UkTime.Utc(startUtc)) == Date)
            return null;

        return who is null ? WorkingHours.Within(Starts!.Value, Ends!.Value) : WorkingHours.Within(Starts!.Value, Ends!.Value, who);
    }
}

/// <summary>
/// Staff and site hours as loaded for some people and dates (dated rows: <see cref="IDatedRow"/>), worked out into each
/// person's <see cref="WorkingTime"/> on a date:
/// <list type="bullet">
/// <item>A person with hours of their own valid on the date works the row for its weekday, if any, at that row's site;
/// one with none works the default (<see cref="WorkingHours"/>: Monday to Friday, 08:00 to 18:00) at no particular site.</item>
/// <item>A site's opening hours valid on the date, or the default if it has none, bound its staff's day: their day is
/// the part of their hours it's open (none, if it's closed that weekday).</item>
/// <item>A day closed everywhere closes everyone; a site's closed day closes the staff working there that day.</item>
/// </list>
/// </summary>
public sealed class WorkingTimeSet(IEnumerable<StaffHours> staffHours, IEnumerable<SiteHours> siteHours)
{
    private readonly IReadOnlyList<StaffHours> _staffHours = staffHours.ToList();
    private readonly IReadOnlyList<SiteHours> _siteHours = siteHours.ToList();

    /// <summary>Nobody has hours of their own: everyone works the default.</summary>
    public static WorkingTimeSet None { get; } = new([], []);

    public IReadOnlyList<StaffHours> StaffHours => _staffHours;

    public IReadOnlyList<SiteHours> SiteHours => _siteHours;

    /// <summary>A person's working day on a date (see the class).</summary>
    public WorkingTime For(Guid staffUserId, DateOnly date, ClosedDaySet closedDays)
    {
        var own = _staffHours.Where(h => h.StaffUserId == staffUserId && h.IsValidOn(date)).ToList();
        if (own.Count == 0)
        {
            var isDefaultDay = WorkingHours.IsDefaultDay(date.DayOfWeek);
            return new WorkingTime(
                date,
                isDefaultDay ? WorkingHours.Start : null,
                isDefaultDay ? WorkingHours.End : null,
                null,
                closedDays.NameOn(date),
                false);
        }

        var row = own.Where(h => h.DayOfWeek == date.DayOfWeek).MaxBy(h => h.ValidFrom);
        if (row is null)
            return new WorkingTime(date, null, null, null, closedDays.NameOn(date), true);

        // Their day is the part of their hours their site is open.
        var opening = SiteOpening(row.SiteId, date);
        var starts = opening is { } o && o.Opens > row.Starts ? o.Opens : row.Starts;
        var ends = opening is { } c && c.Closes < row.Ends ? c.Closes : row.Ends;
        var works = opening is not null && starts < ends;
        return new WorkingTime(
            date, works ? starts : null, works ? ends : null, row.SiteId, closedDays.NameAt(date, row.SiteId), true);
    }

    /// <summary>
    /// When a site is open on a date (its own hours valid then, or the default's if it has none), or null if it's
    /// closed that weekday. Closed days aside.
    /// </summary>
    public (TimeOnly Opens, TimeOnly Closes)? SiteOpening(Guid siteId, DateOnly date) =>
        SiteOpening(siteId, date.DayOfWeek, date);

    /// <summary>
    /// When a site is open on a day of the week, by its hours valid on <paramref name="validOn"/> (or the default's if it
    /// has none then), or null if it's closed that day.
    /// </summary>
    public (TimeOnly Opens, TimeOnly Closes)? SiteOpening(Guid siteId, DayOfWeek day, DateOnly validOn) =>
        Opening(_siteHours.Where(h => h.SiteId == siteId && h.IsValidOn(validOn)).ToList(), day);

    /// <summary>When a site with these rows (one date's) is open on a day of the week: the default's when there are none.</summary>
    public static (TimeOnly Opens, TimeOnly Closes)? Opening(IReadOnlyCollection<SiteHours> rows, DayOfWeek day)
    {
        if (rows.Count == 0)
            return WorkingHours.IsDefaultDay(day) ? (WorkingHours.Start, WorkingHours.End) : null;

        var row = rows.Where(h => h.DayOfWeek == day).MaxBy(h => h.ValidFrom);
        return row is null ? null : (row.Opens, row.Closes);
    }
}
