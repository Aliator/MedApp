namespace MedApp.Contracts.Pathways.Timeline.Responses;

/// <summary>A pathway that isn't closed, and when it's due.</summary>
public sealed class PatientTimelineUpcomingResponse
{
    public Guid PathwayId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateOnly DueDate { get; set; }
    public bool IsOverdue { get; set; }
}
