namespace MedApp.Contracts.Calendar.Requests;

/// <summary>A meeting, to add or to replace one with: inside one working day, not on a past day, clash-free for everyone.</summary>
public sealed class MeetingRequest
{
    /// <summary>At most 100 characters.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional, at most 500 characters. About staff, never a patient.</summary>
    public string? Notes { get; set; }

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    /// <summary>Who's going: at least one member of staff (the organiser isn't added unless listed).</summary>
    public List<Guid> AttendeeUserIds { get; set; } = [];

    /// <summary>
    /// When changing a meeting (required then; ignored when adding one): its <c>LastUpdated</c> as the caller last read
    /// it. If anyone has changed the meeting since, the change is refused (409), rather than undoing theirs.
    /// </summary>
    public DateTime? ExpectedLastUpdated { get; set; }
}
