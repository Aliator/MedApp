namespace MedApp.Contracts.Pathways.Timeline.Responses;

/// <summary>One page of a patient's timeline, newest first.</summary>
public sealed class PatientTimelinePageResponse
{
    public List<PatientTimelineEntryResponse> Items { get; set; } = [];

    /// <summary>Pass as <c>Before</c> for the next (older) page; null when there's nothing older.</summary>
    public string? NextCursor { get; set; }
}
