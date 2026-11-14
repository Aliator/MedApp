namespace MedApp.Contracts.Procedures.PatientProcedures.Responses;

/// <summary>A procedure as a table row: lists, a patient's procedures, and task lists.</summary>
public sealed class PatientProcedureSummaryResponse
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
    public int StageCount { get; set; }

    /// <summary>The name of the stage being worked on, or null when every stage is finished.</summary>
    public string? CurrentStageName { get; set; }

    public List<string> Assignees { get; set; } = [];
    public DateTime LastUpdated { get; set; }
}
