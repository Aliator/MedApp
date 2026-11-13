namespace MedApp.Domain.Procedures.PatientProcedures;

public sealed class PatientProcedureStage
{
    public Guid Id { get; init; }

    public Guid PatientProcedureId { get; set; }
    public PatientProcedure PatientProcedure { get; set; } = null!;

    public Guid StageDefinitionId { get; set; }
    public PatientProcedureStageDefinition StageDefinition { get; set; } = null!;

    public int StageOrder { get; set; }

    public bool IsCompleted { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public Guid? CompletedByUserId { get; set; }

    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdated { get; set; }
}