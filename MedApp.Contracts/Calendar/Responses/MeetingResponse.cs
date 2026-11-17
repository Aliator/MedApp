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

    /// <summary>Null once the organiser's account is deleted.</summary>
    public string? OrganiserUsername { get; set; }

    /// <summary>Who's going, by username.</summary>
    public List<MeetingAttendeeResponse> Attendees { get; set; } = [];

    /// <summary>Whether the signed-in user can change or cancel it: an admin, and it isn't on a past day.</summary>
    public bool CanEdit { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime LastUpdated { get; set; }
}

public sealed class MeetingAttendeeResponse
{
    public Guid UserId { get; set; }

    /// <summary>Null once their account is deleted.</summary>
    public string? Username { get; set; }
}
