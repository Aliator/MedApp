namespace MedApp.Contracts.Tasks.PatientTaskStageDefinitions.Requests;

public sealed class CreatePatientTaskStageDefinitionRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
}