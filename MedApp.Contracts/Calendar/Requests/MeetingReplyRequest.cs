namespace MedApp.Contracts.Calendar.Requests;

/// <summary>The signed-in member of staff's answer to a meeting they're going to.</summary>
public sealed class MeetingReplyRequest
{
    /// <summary>"Accepted" or "Declined".</summary>
    public string Reply { get; set; } = string.Empty;
}
