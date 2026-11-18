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

    /// <summary>Whether staff may skip this stage, with a reason, when it's current. Copied onto each pathway's stage.</summary>
    public bool IsOptional { get; set; }

    /// <summary>
    /// Whether this stage is a branch point: finishing it asks the clinician to choose one of the branches that follow
    /// (up to the next branch point), and the other branches' stages are skipped. Copied onto each pathway's stage.
    /// </summary>
    public bool ChoosesBranch { get; set; }

    /// <summary>
    /// The branch this stage belongs to (an admin-written name, e.g. "Treatment"), or null for a stage that always runs.
    /// Only after a branch point. Copied onto each pathway's stage.
    /// </summary>
    public string? Branch { get; set; }
}
