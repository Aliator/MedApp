namespace MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Requests;

public sealed class UpdatePatientProcedureStageTemplateRequest
{
    public string? Name { get; set; }
    public List<Guid>? StageDefinitionIdsInOrder { get; set; }
}