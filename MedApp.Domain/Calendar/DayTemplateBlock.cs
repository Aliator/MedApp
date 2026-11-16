namespace MedApp.Domain.Calendar;

/// <summary>
/// One block of a day template, at UK clock times, so a 12:00 lunch is at 12:00 UK time on both sides of a clock
/// change. It applies from <see cref="ValidFrom"/> to <see cref="ValidTo"/> (see <see cref="IDatedRow"/>).
/// </summary>
public sealed class DayTemplateBlock : IDatedRow
{
    public Guid Id { get; init; }

    public Guid DayTemplateId { get; set; }
    public DayTemplate DayTemplate { get; set; } = null!;

    public Guid ActivityTypeId { get; set; }
    public CalendarActivityType ActivityType { get; set; } = null!;

    /// <summary>UK clock time.</summary>
    public TimeOnly StartTime { get; set; }

    /// <summary>UK clock time.</summary>
    public TimeOnly EndTime { get; set; }

    public string Note { get; set; } = string.Empty;

    public DateOnly ValidFrom { get; init; }
    public DateOnly? ValidTo { get; set; }
}
