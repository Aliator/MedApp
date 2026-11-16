namespace MedApp.Domain.Calendar;

/// <summary>
/// A named working week: a day template (or none) for each weekday, Monday to Friday. A member of staff's default
/// week template fills their calendar automatically. Its days are dated, like a day template's blocks.
/// </summary>
public sealed class WeekTemplate
{
    public Guid Id { get; init; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Archived week templates can't be chosen again, but stay the default of whoever has them until it's changed.
    /// One that has ever been anyone's default is archived, never deleted.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>Every version of its days; a weekday with no row valid on a date has no day template that day.</summary>
    public List<WeekTemplateDay> Days { get; set; } = [];
}
