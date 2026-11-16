namespace MedApp.Domain.Calendar;

/// <summary>
/// One of the preset kinds of blocked time on a calendar, such as Lunch or Training. Admins manage the list; one
/// that's in use (by a block or a template block) is archived, never deleted.
/// </summary>
public sealed class CalendarActivityType
{
    public Guid Id { get; init; }

    public string Name { get; set; } = string.Empty;

    /// <summary>A colour name from the fixed palette (e.g. "green"), which the UI turns into a theme colour.</summary>
    public string Colour { get; set; } = string.Empty;

    /// <summary>Archived types can't be chosen for new blocks; blocks that have one keep it.</summary>
    public bool IsArchived { get; set; }
}
