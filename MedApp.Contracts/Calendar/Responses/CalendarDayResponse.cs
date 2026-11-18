namespace MedApp.Contracts.Calendar.Responses;

/// <summary>One date on a calendar.</summary>
public sealed class CalendarDayResponse
{
    public DateOnly Date { get; set; }

    /// <summary>
    /// One of the person's working days (their own hours, or the default Monday to Friday), and not closed (everywhere,
    /// or at their site that day). Other days are shown greyed: nothing new can be booked, blocked or arranged on them.
    /// </summary>
    public bool IsWorkingDay { get; set; }

    /// <summary>
    /// The closed day's name ("Christmas Day") when the clinic is closed that day; null otherwise. Anything already on
    /// a day that later closed stays, and is shown flagged.
    /// </summary>
    public string? ClosedDayName { get; set; }

    /// <summary>Before today (UK): its blocks and template can't be changed.</summary>
    public bool IsPast { get; set; }

    public bool IsToday { get; set; }

    /// <summary>
    /// The start of the person's working day on this date, in UTC (08:00 UK time by default); on a day they don't work,
    /// the default's.
    /// </summary>
    public DateTime WorkStartUtc { get; set; }

    /// <summary>The end of the person's working day on this date, in UTC (18:00 UK time by default).</summary>
    public DateTime WorkEndUtc { get; set; }

    /// <summary>It's one of the person's days (their site open that day), whether or not it's closed.</summary>
    public bool HasHours { get; set; }

    /// <summary>The site their hours name that day, if any.</summary>
    public Guid? SiteId { get; set; }

    /// <summary>
    /// Where the grid starts on this date, in UTC: the same UK clock time on every day of one calendar (or team), so the
    /// rows line up; times outside <see cref="WorkStartUtc"/> to <see cref="WorkEndUtc"/> are shown unavailable.
    /// </summary>
    public DateTime GridStartUtc { get; set; }

    /// <summary>Where the grid ends on this date, in UTC.</summary>
    public DateTime GridEndUtc { get; set; }

    /// <summary>The day template in force, if any.</summary>
    public Guid? DayTemplateId { get; set; }

    public string? DayTemplateName { get; set; }

    /// <summary>The day has a day template of its own (or none), instead of the week template's.</summary>
    public bool IsDayTemplateOverridden { get; set; }
}
