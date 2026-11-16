namespace MedApp.Contracts.Calendar.Responses;

/// <summary>
/// Blocked time on a calendar: a stored block (<see cref="BlockId"/>) or a day template's block on this date
/// (<see cref="TemplateBlockId"/>), which can be changed or removed for this day alone.
/// </summary>
public sealed class CalendarBlockResponse
{
    /// <summary>A stored block's ID; null for a template block.</summary>
    public Guid? BlockId { get; set; }

    /// <summary>A template block's ID; null for a stored block.</summary>
    public Guid? TemplateBlockId { get; set; }

    public bool IsFromTemplate { get; set; }

    public Guid StaffUserId { get; set; }

    /// <summary>The UK date it's on.</summary>
    public DateOnly Date { get; set; }

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public Guid ActivityTypeId { get; set; }

    public string ActivityName { get; set; } = string.Empty;

    /// <summary>The activity type's palette colour, e.g. "green".</summary>
    public string Colour { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    /// <summary>Whether the signed-in user can change it: they can change this calendar and it isn't on a past day.</summary>
    public bool CanEdit { get; set; }

    /// <summary>
    /// It overlaps an appointment (a template changed under one that was already booked). Both show, the appointment
    /// on top; nothing is moved by the app.
    /// </summary>
    public bool OverlapsAppointment { get; set; }
}
