namespace MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;

public sealed class PathwayTemplateResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsArchived { get; set; }

    /// <summary>The team it belongs to.</summary>
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;

    /// <summary>How long its patients' records are kept before review: Standard, Oncology, Maternity or MentalHealth.</summary>
    public string RetentionClass { get; set; } = "Standard";

    /// <summary>How many pathways were created from it. A template that has been used can only be archived.</summary>
    public int PathwayCount { get; set; }

    public List<PathwayTemplateStageResponse> Stages { get; set; } = [];

    /// <summary>The stage definition IDs in order (the same as <see cref="Stages"/>), for editing.</summary>
    public List<Guid> StageDefinitionIdsInOrder { get; set; } = [];

    /// <summary>The position (from 1) of the stage that stops the waiting-time clock, or null for none.</summary>
    public int? ClockStopStageOrder { get; set; }

    /// <summary>Its routes (MC8), by order: each stage to the next for a template written as a list.</summary>
    public List<PathwayTemplateLinkResponse> Links { get; set; } = [];

    /// <summary>The branches staff may add to its pathways part-way (MC10), in order.</summary>
    public List<PathwayTemplateBranchResponse> AddableBranches { get; set; } = [];
}

public sealed class PathwayTemplateStageResponse
{
    public int StageOrder { get; set; }
    public Guid StageDefinitionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>The stage definition's SNOMED CT code and term, or null.</summary>
    public string? SnomedCode { get; set; }
    public string? SnomedTerm { get; set; }

    public bool IsArchived { get; set; }

    /// <summary>The stage definition's: None, Suggested or Required.</summary>
    public string VisitRequirement { get; set; } = string.Empty;

    /// <summary>Staff may skip it, with a reason, while it's the current stage.</summary>
    public bool IsOptional { get; set; }

    /// <summary>A choice node's kind (Assessment or LabResult), or null for a plain stage.</summary>
    public string? ChoiceKind { get; set; }

    /// <summary>A choice node's outcomes, in order; null for a plain stage.</summary>
    public List<PathwayTemplateOutcomeResponse>? Outcomes { get; set; }

    /// <summary>Finishing this stage stops a referral's waiting-time clock.</summary>
    public bool StopsClock { get; set; }

    /// <summary>The team that works it (MC7): null for the template's own team, or the lab.</summary>
    public Guid? OwnerTeamId { get; set; }
    public string? OwnerTeamName { get; set; }
}

/// <summary>One of a choice node's outcomes: its name, and the stages it leads to, by order.</summary>
public sealed class PathwayTemplateOutcomeResponse
{
    public string Name { get; set; } = string.Empty;
    public List<int> NextStageOrders { get; set; } = [];
}

/// <summary>A link between two of a template's stages, by order.</summary>
public sealed class PathwayTemplateLinkResponse
{
    public int FromStageOrder { get; set; }
    public int ToStageOrder { get; set; }
}

/// <summary>An addable branch (MC10): its stages, where it may attach, and whether it repeats.</summary>
public sealed class PathwayTemplateBranchResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<PathwayTemplateBranchStageResponse> Stages { get; set; } = [];
    public List<int> AttachAfterStageOrders { get; set; } = [];
    public bool IsRepeatable { get; set; }
}

/// <summary>One stage of an addable branch.</summary>
public sealed class PathwayTemplateBranchStageResponse
{
    public Guid StageDefinitionId { get; set; }
    public string Name { get; set; } = string.Empty;
}
