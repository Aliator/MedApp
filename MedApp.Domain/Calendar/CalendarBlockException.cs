namespace MedApp.Domain.Calendar;

/// <summary>
/// A template block left off one date of one member of staff's calendar, because it was changed (a stored
/// <see cref="CalendarBlock"/> takes its place) or removed for that day. Later edits to the template keep it, as long
/// as they don't change that block.
/// </summary>
public sealed class CalendarBlockException
{
    public Guid Id { get; init; }

    public Guid StaffUserId { get; set; }

    /// <summary>The UK date.</summary>
    public DateOnly Date { get; set; }

    public Guid DayTemplateBlockId { get; set; }
    public DayTemplateBlock DayTemplateBlock { get; set; } = null!;
}
