namespace MedApp.Contracts.Tasks.PatientTasks.Responses;

public sealed class PatientTaskStageResponse
{
    public Guid Id { get; set; }

    public Guid StageDefinitionId { get; set; }

    public int StageOrder { get; set; }

    public bool IsCompleted { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public Guid? CompletedByUserId { get; set; }

    public string StageName { get; set; } = string.Empty;
    public string StageDescription { get; set; } = string.Empty;
    public string StageInstructions { get; set; } = string.Empty;
}