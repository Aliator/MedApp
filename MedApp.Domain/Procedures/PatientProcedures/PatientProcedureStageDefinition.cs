namespace MedApp.Domain.Procedures.PatientProcedures;

/// <summary>A reusable stage, such as "Consultation", that templates list (possibly more than once).</summary>
public sealed class PatientProcedureStageDefinition
{
    public Guid Id { get; init; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Instructions { get; set; } = string.Empty;

    /// <summary>Archived definitions can't be added to templates. A definition in use is archived, never deleted.</summary>
    public bool IsArchived { get; set; }
}
