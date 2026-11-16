namespace MedApp.Domain.Calendar;

/// <summary>
/// Something an admin arranges for several members of staff, such as a team meeting: busy time on each attendee's
/// calendar. It sits inside one working day, like a block. Its notes are about staff, never patients. Cancelling it
/// deletes it.
/// </summary>
public sealed class Meeting
{
    public Guid Id { get; init; }

    public string Title { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    /// <summary>The admin who arranged it. No foreign key to the users table, as with other staff IDs.</summary>
    public Guid OrganiserUserId { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime LastUpdated { get; set; }

    /// <summary>Who's going: at least one, each once.</summary>
    public List<MeetingAttendee> Attendees { get; set; } = [];

    /// <summary>Changes on every save, so the later of two overlapping changes is refused (a 409).</summary>
    public byte[] RowVersion { get; set; } = [];
}
