namespace MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Requests;

public sealed class CreatePatientProcedureStageTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<Guid> StageDefinitionIdsInOrder { get; set; } = Array.Empty<Guid>();
}