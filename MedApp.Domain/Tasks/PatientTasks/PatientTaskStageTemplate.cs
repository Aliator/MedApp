namespace MedApp.Domain.Tasks.PatientTasks;

public sealed class PatientTaskStageTemplate
{
    public Guid Id { get; init; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdated { get; set; }

    public List<PatientTaskStageTemplateMap> Maps { get; set; } = [];
}