namespace MedApp.Contracts.Tasks.PatientTaskStageDefinitions.Requests;

public sealed class UpdatePatientTaskStageDefinitionRequest
{
    public Guid StageDefinitionId { get; set; }

    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Instructions { get; set; }

    public bool? IsActive { get; set; }
}