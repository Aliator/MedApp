namespace MedApp.Domain.Calendar;

/// <summary>
/// A member of staff's default week template over a range of dates (see <see cref="IDatedRow"/>): changing it applies
/// from that day, and past days keep the one they had.
/// </summary>
public sealed class StaffWeekTemplate : IDatedRow
{
    public Guid Id { get; init; }

    /// <summary>No foreign key to the users table, as with pathway assignments.</summary>
    public Guid StaffUserId { get; set; }

    public Guid WeekTemplateId { get; set; }
    public WeekTemplate WeekTemplate { get; set; } = null!;

    public DateOnly ValidFrom { get; init; }
    public DateOnly? ValidTo { get; set; }
}
