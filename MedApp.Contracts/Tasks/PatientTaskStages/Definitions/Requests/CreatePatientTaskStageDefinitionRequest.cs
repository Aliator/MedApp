namespace MedApp.Contracts.Tasks.PatientTaskStages.Definitions.Requests;

public sealed class CreatePatientTaskStageDefinitionRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
}