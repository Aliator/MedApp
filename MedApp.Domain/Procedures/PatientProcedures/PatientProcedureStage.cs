namespace MedApp.Domain.Procedures.PatientProcedures;

/// <summary>
/// One stage of a patient's procedure. Its name, description, instructions and SNOMED CT code are copied from the stage
/// definition when the procedure is created, so later edits to the definition don't rewrite a record of work.
/// Stages are worked in order: started, notes written, then finished, which locks them until an admin unlocks.
/// </summary>
public sealed class PatientProcedureStage
{
    public Guid Id { get; init; }

    public Guid PatientProcedureId { get; set; }
    public PatientProcedure PatientProcedure { get; set; } = null!;

    public Guid StageDefinitionId { get; set; }
    public PatientProcedureStageDefinition StageDefinition { get; set; } = null!;

    public int StageOrder { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;

    /// <summary>The definition's SNOMED CT code and term, copied with its name when the procedure was created.</summary>
    public string? SnomedCode { get; set; }
    public string? SnomedTerm { get; set; }
    /// <summary>Copied from the stage definition: whether the stage involves a visit (an appointment).</summary>
    public VisitRequirement VisitRequirement { get; set; }

    /// <summary>The staff member's notes for this stage, editable until it's finished (or while an admin has it unlocked).</summary>
    public string Notes { get; set; } = string.Empty;

    public DateTime? StartedAtUtc { get; set; }
    public Guid? StartedByUserId { get; set; }

    public bool IsCompleted { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public Guid? CompletedByUserId { get; set; }

    /// <summary>A finished stage is locked. An admin can unlock it so it can be corrected and finished again.</summary>
    public bool IsLocked { get; set; }

    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdated { get; set; }
}
