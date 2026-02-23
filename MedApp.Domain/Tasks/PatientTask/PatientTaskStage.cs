namespace MedApp.Domain.Tasks.PatientTask;

public sealed class PatientTaskStage
{
    public Guid Id { get; init; }

    public Guid PatientTaskId { get; set; }
    public PatientTask PatientTask { get; set; } = null!;

    public Guid StageDefinitionId { get; set; }
    public PatientTaskStageDefinition StageDefinition { get; set; } = null!;

    public int StageOrder { get; set; }

    public bool IsCompleted { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public Guid? CompletedByUserId { get; set; }

    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdated { get; set; }
}