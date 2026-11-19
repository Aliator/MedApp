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
    /// A choice node's kind (MC10b), or null for a plain stage: finishing it records the clinician's outcome, one of
    /// <see cref="Outcomes"/>, and only that outcome's stages follow. Copied into each pathway's plan.
    /// </summary>
    public StageChoiceKind? ChoiceKind { get; set; }

    /// <summary>A choice node's outcomes, in the template's order (empty for a plain stage). Stored as JSON.</summary>
    public List<PathwayTemplateOutcome> Outcomes { get; set; } = [];

    /// <summary>
    /// The team that works this stage (MC7, the relay): null for the template's own team, or the lab. Copied onto each
    /// pathway's stage; reaching it hands the pathway over.
    /// </summary>
    public Guid? OwnerTeamId { get; set; }
    public Teams.Team? OwnerTeam { get; set; }

    /// <summary>
    /// Finishing this stage stops the waiting-time clock of a pathway accepted from a referral (at most one per
    /// template, chosen by admins). Copied onto each new pathway's stage, so changing it affects only new pathways.
    /// </summary>
    public bool StopsClock { get; set; }
}
