namespace MedApp.Domain.Tasks.PatientTask;

public sealed class PatientTaskStageDefinition
{
    public Guid Id { get; init; }
    public string Name { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;

    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdated { get; set; }
}