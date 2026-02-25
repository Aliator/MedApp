namespace MedApp.Contracts.Tasks.PatientTaskStages.Templates.Responses;

public sealed class PatientTaskStageTemplateResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public List<Guid> StageDefinitionIdsInOrder { get; set; } = [];
}