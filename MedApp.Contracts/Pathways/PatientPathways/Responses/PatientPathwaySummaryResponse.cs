namespace MedApp.Contracts.Pathways.PatientPathways.Responses;

/// <summary>A pathway as a table row: lists, a patient's pathways, and task lists.</summary>
public sealed class PatientPathwaySummaryResponse
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public DateOnly DueDate { get; set; }
    public DateTime DueDateUtc { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsOverdue { get; set; }
    public int CompletedStageCount { get; set; }

    /// <summary>How many stages were skipped (an optional stage, or another branch chosen).</summary>
    public int SkippedStageCount { get; set; }
    public int StageCount { get; set; }

    /// <summary>The name of the stage being worked on, or null when every stage is finished.</summary>
    public string? CurrentStageName { get; set; }

    public List<string> Assignees { get; set; } = [];
    public DateTime LastUpdated { get; set; }
}
