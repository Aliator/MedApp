namespace MedApp.Contracts.Calendar.Requests;

/// <summary>The signed-in member of staff's answer to a meeting they're going to.</summary>
public sealed class MeetingReplyRequest
{
    /// <summary>"Accepted" or "Declined".</summary>
    public string Reply { get; set; } = string.Empty;

    /// <summary>
    /// The meeting's start and end as the person saw them (required): a reply is about that time, so if the meeting has
    /// moved since, it's refused (409) rather than counting for a time they didn't see.
    /// </summary>
    public DateTime? ExpectedStartUtc { get; set; }

    public DateTime? ExpectedEndUtc { get; set; }
}
