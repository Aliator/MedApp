namespace MedApp.Contracts.Tasks.PatientTaskStages.Definitions.Responses;

public sealed class PatientTaskStageDefinitionResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
}