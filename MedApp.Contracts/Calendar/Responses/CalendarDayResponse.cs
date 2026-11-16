namespace MedApp.Contracts.Calendar.Responses;

/// <summary>One date on a calendar.</summary>
public sealed class CalendarDayResponse
{
    public DateOnly Date { get; set; }

    /// <summary>Monday to Friday. Weekends are shown greyed: nothing can be booked or blocked on them.</summary>
    public bool IsWorkingDay { get; set; }

    /// <summary>Before today (UK): its blocks and template can't be changed.</summary>
    public bool IsPast { get; set; }

    public bool IsToday { get; set; }

    /// <summary>08:00 UK time on this date, in UTC.</summary>
    public DateTime WorkStartUtc { get; set; }

    /// <summary>18:00 UK time on this date, in UTC.</summary>
    public DateTime WorkEndUtc { get; set; }

    /// <summary>The day template in force, if any.</summary>
    public Guid? DayTemplateId { get; set; }

    public string? DayTemplateName { get; set; }

    /// <summary>The day has a day template of its own (or none), instead of the week template's.</summary>
    public bool IsDayTemplateOverridden { get; set; }
}
