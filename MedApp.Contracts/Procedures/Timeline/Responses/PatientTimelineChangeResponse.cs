namespace MedApp.Contracts.Procedures.Timeline.Responses;

/// <summary>
/// One field a change edited: title, notes, dueDate, priority or stageNotes. Due dates and priorities come with
/// their values; free text (title, notes, stageNotes) only for admins, and otherwise just says it changed.
/// </summary>
public sealed class PatientTimelineChangeResponse
{
    public string Field { get; set; } = string.Empty;

    public string? From { get; set; }

    public string? To { get; set; }
}
