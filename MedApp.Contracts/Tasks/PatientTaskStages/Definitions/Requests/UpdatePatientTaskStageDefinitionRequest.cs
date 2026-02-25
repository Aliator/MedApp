namespace MedApp.Contracts.Tasks.PatientTaskStages.Definitions.Requests;

public sealed class UpdatePatientTaskStageDefinitionRequest
{
    public Guid StageDefinitionId { get; set; }

    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Instructions { get; set; }
}