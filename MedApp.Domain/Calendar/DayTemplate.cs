namespace MedApp.Domain.Calendar;

/// <summary>
/// A named day of blocks, such as "Clinic day" with a lunch at 12:00, that week templates and day overrides apply to
/// calendars. Its blocks are dated, so an edit applies from the day it's made and past days keep what they had.
/// </summary>
public sealed class DayTemplate
{
    public Guid Id { get; init; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Archived day templates can't be chosen again, but keep applying where they already do. One that a week template
    /// or day override has ever referred to is archived, never deleted.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>Every version of its blocks; the ones in force on a date are those valid on it.</summary>
    public List<DayTemplateBlock> Blocks { get; set; } = [];
}
