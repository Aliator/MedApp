namespace MedApp.Contracts.Pathways.PatientPathwayStages.Definitions.Responses;

public sealed class PatientPathwayStageDefinitionResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;

    /// <summary>The SNOMED CT concept the stage records, or null.</summary>
    public string? SnomedCode { get; set; }

    /// <summary>Its display term, or null.</summary>
    public string? SnomedTerm { get; set; }

    /// <summary>Whether the stage involves a visit: None, Suggested or Required.</summary>
    public string VisitRequirement { get; set; } = string.Empty;

    /// <summary>The kind of letter the stage needs before it can be finished, by name, or null for none.</summary>
    public string? NeedsLetterKind { get; set; }

    public bool IsArchived { get; set; }

    /// <summary>How many templates use it. A definition in use by a template or pathway can only be archived.</summary>
    public int TemplateCount { get; set; }

    /// <summary>Whether it can be deleted: true only if no template or pathway has ever used it.</summary>
    public bool CanBeDeleted { get; set; }
}
