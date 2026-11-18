namespace MedApp.Domain.Pathways.PatientPathways;

/// <summary>
/// One position in a template's stage list. Keyed by template and position, so the same stage definition can
/// appear more than once (e.g. Consultation, Preparation, Consultation, ...).
/// </summary>
public sealed class PathwayTemplateMap
{
    public Guid TemplateId { get; set; }
    public PathwayTemplate Template { get; set; } = null!;

    public Guid StageDefinitionId { get; set; }
    public PatientPathwayStageDefinition StageDefinition { get; set; } = null!;

    public int StageOrder { get; set; }
}
