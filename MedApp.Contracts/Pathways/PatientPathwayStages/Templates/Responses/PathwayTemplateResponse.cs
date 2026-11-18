namespace MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;

public sealed class PathwayTemplateResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsArchived { get; set; }

    /// <summary>The specialty it belongs to.</summary>
    public Guid SpecialtyId { get; set; }
    public string SpecialtyName { get; set; } = string.Empty;

    /// <summary>How long its patients' records are kept before review: Standard, Oncology, Maternity or MentalHealth.</summary>
    public string RetentionClass { get; set; } = "Standard";

    /// <summary>How many pathways were created from it. A template that has been used can only be archived.</summary>
    public int PathwayCount { get; set; }

    public List<PathwayTemplateStageResponse> Stages { get; set; } = [];

    /// <summary>The stage definition IDs in order (the same as <see cref="Stages"/>), for editing.</summary>
    public List<Guid> StageDefinitionIdsInOrder { get; set; } = [];

    /// <summary>The position (from 1) of the stage that stops the waiting-time clock, or null for none.</summary>
    public int? ClockStopStageOrder { get; set; }
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

    /// <summary>A branch point: finishing it, the clinician chooses one of the branches that follow.</summary>
    public bool ChoosesBranch { get; set; }

    /// <summary>The branch it belongs to, or null for a stage that always runs.</summary>
    public string? Branch { get; set; }
    /// <summary>Finishing this stage stops a referral's waiting-time clock.</summary>
    public bool StopsClock { get; set; }
}
