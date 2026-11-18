namespace MedApp.Domain.Calendar;

/// <summary>A member of staff going to a meeting. No foreign key to the users table, as with other staff IDs.</summary>
public sealed class MeetingAttendee
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    public Guid StaffUserId { get; set; }

    /// <summary>Their answer to the invitation, which only they give: none until they reply, and none again once it moves.</summary>
    public MeetingReply Reply { get; set; }

    /// <summary>When they last replied; null while there's no reply.</summary>
    public DateTime? RepliedAtUtc { get; set; }
}
