namespace MedApp.Contracts.Calendar.Responses;

/// <summary>
/// A meeting an admin arranged for several members of staff: busy time on each attendee's calendar. Its notes are about
/// staff, never patients.
/// </summary>
public sealed class MeetingResponse
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    /// <summary>The UK date it's on.</summary>
    public DateOnly Date { get; set; }

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public Guid OrganiserUserId { get; set; }

    /// <summary>Whether it's a specialty's MDT (multidisciplinary team meeting), which pathways can be listed for.</summary>
    public bool IsMdt { get; set; }

    /// <summary>The MDT's specialty; null for an ordinary meeting.</summary>
    public Guid? SpecialtyId { get; set; }

    /// <summary>Null once the organiser's account is deleted.</summary>
    public string? OrganiserUsername { get; set; }

    /// <summary>Who's going, by username.</summary>
    public List<MeetingAttendeeResponse> Attendees { get; set; } = [];

    /// <summary>Whether the signed-in user can change or cancel it: an admin, and it isn't on a past day.</summary>
    public bool CanEdit { get; set; }

    /// <summary>The signed-in user's own reply ("NoReply", "Accepted" or "Declined"), or null when they aren't going.</summary>
    public string? MyReply { get; set; }

    /// <summary>Whether the signed-in user can accept or decline it: they're going, and it hasn't ended.</summary>
    public bool CanReply { get; set; }

    /// <summary>
    /// On a calendar: whether an appointment on it overlaps the meeting (one recorded after the event may, as what
    /// happened, happened). Always false outside a calendar.
    /// </summary>
    public bool OverlapsAppointment { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime LastUpdated { get; set; }
}

public sealed class MeetingAttendeeResponse
{
    public Guid UserId { get; set; }

    /// <summary>Null once their account is deleted.</summary>
    public string? Username { get; set; }

    /// <summary>Their answer: "NoReply" (none yet, or the meeting moved since), "Accepted" or "Declined".</summary>
    public string Reply { get; set; } = string.Empty;

    /// <summary>When they last replied; null while there's no reply.</summary>
    public DateTime? RepliedAtUtc { get; set; }
}
