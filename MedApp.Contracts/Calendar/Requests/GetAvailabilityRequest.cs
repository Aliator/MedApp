namespace MedApp.Contracts.Calendar.Requests;

/// <summary>Query string for <c>GET api/calendar/availability</c>: when several people are all free.</summary>
public sealed class GetAvailabilityRequest
{
    /// <summary>At least one member of staff.</summary>
    public List<Guid> StaffUserIds { get; set; } = [];

    /// <summary>The first UK date.</summary>
    public DateOnly From { get; set; }

    /// <summary>The last UK date (inclusive), at most 31 days after the first.</summary>
    public DateOnly To { get; set; }

    /// <summary>How long the meeting would be: a multiple of 5 minutes, at most the working day.</summary>
    public int DurationMinutes { get; set; }

    /// <summary>A meeting being moved, left out of everyone's busy time.</summary>
    public Guid? ExceptMeetingId { get; set; }
}
