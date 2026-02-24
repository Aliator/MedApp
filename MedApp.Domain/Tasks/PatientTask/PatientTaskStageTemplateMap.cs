namespace MedApp.Domain.Tasks.PatientTask;

public sealed class PatientTaskStageTemplateMap
{
    public Guid TemplateId { get; set; }
    public PatientTaskStageTemplate Template { get; set; } = null!;

    public Guid StageDefinitionId { get; set; }
    public PatientTaskStageDefinition StageDefinition { get; set; } = null!;

    public int StageOrder { get; set; }
}