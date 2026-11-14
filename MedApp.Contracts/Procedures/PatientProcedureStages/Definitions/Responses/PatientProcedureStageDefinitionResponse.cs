namespace MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Responses;

public sealed class PatientProcedureStageDefinitionResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public bool IsArchived { get; set; }

    /// <summary>How many templates use it. A definition in use by a template or procedure can only be archived.</summary>
    public int TemplateCount { get; set; }

    /// <summary>Whether it can be deleted: true only if no template or procedure has ever used it.</summary>
    public bool CanBeDeleted { get; set; }
}
