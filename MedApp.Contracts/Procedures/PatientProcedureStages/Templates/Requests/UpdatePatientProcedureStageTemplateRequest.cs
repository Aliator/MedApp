namespace MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Requests;

/// <summary>Edits a template. Only the fields that are set change. Existing procedures keep their copy of the stages.</summary>
public sealed class UpdatePatientProcedureStageTemplateRequest
{
    public string? Name { get; set; }

    /// <summary>The template's stages in order; the same definition may appear more than once.</summary>
    public List<Guid>? StageDefinitionIdsInOrder { get; set; }
}
