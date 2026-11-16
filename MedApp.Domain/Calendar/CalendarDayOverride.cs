namespace MedApp.Domain.Calendar;

/// <summary>
/// One date on a member of staff's calendar using a day template of its own (or none) instead of the one their week
/// template gives it. Removing it goes back to the week's. One per person and date.
/// </summary>
public sealed class CalendarDayOverride
{
    public Guid Id { get; init; }

    public Guid StaffUserId { get; set; }

    /// <summary>The UK date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>The day template that date uses; null for none.</summary>
    public Guid? DayTemplateId { get; set; }
    public DayTemplate? DayTemplate { get; set; }
}
