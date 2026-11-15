namespace MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Requests;

public sealed class CreatePatientProcedureStageTemplateRequest
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The template's stages in order; the same definition may appear more than once.</summary>
    public IReadOnlyList<Guid> StageDefinitionIdsInOrder { get; set; } = Array.Empty<Guid>();
}
