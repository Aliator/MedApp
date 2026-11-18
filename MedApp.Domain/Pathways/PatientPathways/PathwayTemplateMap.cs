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

    /// <summary>
    /// Finishing this stage stops the waiting-time clock of a pathway accepted from a referral (at most one per
    /// template, chosen by admins). Copied onto each new pathway's stage, so changing it affects only new pathways.
    /// </summary>
    public bool StopsClock { get; set; }
}
