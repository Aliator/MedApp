namespace MedApp.Contracts.Tasks.PatientTaskStages.Templates.Requests;

public sealed class UpdatePatientTaskStageTemplateRequest
{
    public string? Name { get; set; }
    public List<Guid>? StageDefinitionIdsInOrder { get; set; }
}