namespace MedApp.Domain.Calendar;

/// <summary>
/// Time a member of staff has blocked off on their calendar for an activity (Lunch, Training, ...), added by hand or
/// in place of a template block changed for one day. Blocks are about staff, not patients: the note must not hold
/// patient details.
/// </summary>
public sealed class CalendarBlock
{
    public Guid Id { get; init; }

    /// <summary>Whose calendar it's on. No foreign key to the users table, as with pathway assignments.</summary>
    public Guid StaffUserId { get; set; }

    public Guid ActivityTypeId { get; set; }
    public CalendarActivityType ActivityType { get; set; } = null!;

    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }

    public string Note { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }
    public Guid CreatedByUserId { get; init; }
    public DateTime LastUpdated { get; set; }
}
