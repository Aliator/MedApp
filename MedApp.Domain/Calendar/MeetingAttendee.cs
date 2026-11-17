namespace MedApp.Domain.Calendar;

/// <summary>A member of staff going to a meeting. No foreign key to the users table, as with other staff IDs.</summary>
public sealed class MeetingAttendee
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    public Guid StaffUserId { get; set; }
}
