namespace MedApp.Contracts.Tasks.PatientTaskStages.Templates.Requests;

public sealed class CreatePatientTaskStageTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public List<Guid> StageDefinitionIdsInOrder { get; set; } = [];
}