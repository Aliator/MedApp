namespace MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Responses;

public sealed class PatientProcedureStageTemplateResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public List<Guid> StageDefinitionIdsInOrder { get; set; } = [];
}