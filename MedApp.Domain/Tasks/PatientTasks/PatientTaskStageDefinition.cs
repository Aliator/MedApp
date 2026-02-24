namespace MedApp.Domain.Tasks.PatientTasks;

public sealed class PatientTaskStageDefinition
{
    public Guid Id { get; init; }
    public string Name { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
}