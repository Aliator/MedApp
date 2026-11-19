namespace MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Requests;

public sealed class CreatePathwayTemplateRequest
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The team it belongs to (required; not an archived one).</summary>
    public Guid TeamId { get; set; }

    /// <summary>
    /// How long its patients' records are kept before review: Standard (the default: the adult or child period),
    /// Oncology, Maternity or MentalHealth.
    /// </summary>
    public string? RetentionClass { get; set; }

    /// <summary>The template's stages in order; the same definition may appear more than once.</summary>
    public IReadOnlyList<Guid> StageDefinitionIdsInOrder { get; set; } = Array.Empty<Guid>();

    /// <summary>
    /// The position (from 1) of the stage whose finishing stops a referral's waiting-time clock, or null for none (the
    /// clock then stops when the pathway is discharged or cancelled).
    /// </summary>
    public int? ClockStopStageOrder { get; set; }

    /// <summary>
    /// Each stage's options (optional, branch point, branch name), one for each of <see cref="StageDefinitionIdsInOrder"/>
    /// in the same order; leave out for plain stages.
    /// </summary>
    public List<PathwayTemplateStageOptionsRequest>? StageOptions { get; set; }

    /// <summary>
    /// Its routes (MC8): which stage comes after which, by order. Left out, the stages are a chain (a list); a choice
    /// node needs them (its outcomes are routes).
    /// </summary>
    public List<PathwayTemplateLinkRequest>? Links { get; set; }

    /// <summary>The branches staff may add to its pathways part-way (MC10), in order; left out, none.</summary>
    public List<PathwayTemplateBranchRequest>? AddableBranches { get; set; }
}

/// <summary>One template stage's options. The default is a plain stage, which always runs.</summary>
public sealed class PathwayTemplateStageOptionsRequest
{
    /// <summary>Staff may skip it, with a reason, while it's the current stage.</summary>
    public bool IsOptional { get; set; }

    /// <summary>A choice node's kind (Assessment or LabResult), or null for a plain stage (MC10b).</summary>
    public string? ChoiceKind { get; set; }

    /// <summary>A choice node's outcomes, each leading to stages straight after it (or ending the route).</summary>
    public List<PathwayTemplateOutcomeRequest>? Outcomes { get; set; }

    /// <summary>The team that works it (MC7): null for the template's own team, or the lab.</summary>
    public Guid? OwnerTeamId { get; set; }
}

/// <summary>One of a choice node's outcomes: its name, and the stages (by order, straight after it) it leads to.</summary>
public sealed class PathwayTemplateOutcomeRequest
{
    public string? Name { get; set; }
    public List<int>? NextStageOrders { get; set; }
}

/// <summary>A link between two of a template's stages, by order: the second comes after the first.</summary>
public sealed class PathwayTemplateLinkRequest
{
    public int FromStageOrder { get; set; }
    public int ToStageOrder { get; set; }
}

/// <summary>
/// An addable branch (MC10): its name, its stages from the library (a chain), the template stages it may follow (none:
/// any but the last), and whether it can be added more than once. Send its ID back to keep it.
/// </summary>
public sealed class PathwayTemplateBranchRequest
{
    public Guid? Id { get; set; }
    public string? Name { get; set; }
    public List<Guid>? StageDefinitionIds { get; set; }
    public List<int>? AttachAfterStageOrders { get; set; }
    public bool IsRepeatable { get; set; }
}
