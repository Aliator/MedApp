namespace MedApp.Domain.Calendar;

/// <summary>The day template a week template uses on one weekday, over a range of dates (see <see cref="IDatedRow"/>).</summary>
public sealed class WeekTemplateDay : IDatedRow
{
    public Guid Id { get; init; }

    public Guid WeekTemplateId { get; set; }
    public WeekTemplate WeekTemplate { get; set; } = null!;

    /// <summary>Monday (1) to Friday (5).</summary>
    public DayOfWeek DayOfWeek { get; set; }

    public Guid DayTemplateId { get; set; }
    public DayTemplate DayTemplate { get; set; } = null!;

    public DateOnly ValidFrom { get; init; }
    public DateOnly? ValidTo { get; set; }
}
