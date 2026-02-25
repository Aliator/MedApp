namespace MedApp.Domain.Tasks.PatientTasks;

public sealed class PatientTaskStageTemplate
{
    public Guid Id { get; init; }

    public string Name { get; set; } = string.Empty;

    public List<PatientTaskStageTemplateMap> Maps { get; set; } = [];
}