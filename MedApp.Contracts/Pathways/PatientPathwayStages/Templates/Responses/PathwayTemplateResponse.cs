namespace MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;

public sealed class PathwayTemplateResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsArchived { get; set; }

    /// <summary>How many pathways were created from it. A template that has been used can only be archived.</summary>
    public int PathwayCount { get; set; }

    public List<PathwayTemplateStageResponse> Stages { get; set; } = [];

    /// <summary>The stage definition IDs in order (the same as <see cref="Stages"/>), for editing.</summary>
    public List<Guid> StageDefinitionIdsInOrder { get; set; } = [];
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
}
