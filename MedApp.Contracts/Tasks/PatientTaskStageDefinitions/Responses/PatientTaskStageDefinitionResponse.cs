namespace MedApp.Contracts.Tasks.PatientTaskStageDefinitions.Responses;

public sealed class PatientTaskStageDefinitionResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
}