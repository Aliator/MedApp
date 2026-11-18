namespace MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Requests;

public sealed class CreatePathwayTemplateRequest
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The specialty it belongs to (required; not an archived one).</summary>
    public Guid SpecialtyId { get; set; }

    /// <summary>The template's stages in order; the same definition may appear more than once.</summary>
    public IReadOnlyList<Guid> StageDefinitionIdsInOrder { get; set; } = Array.Empty<Guid>();

    /// <summary>
    /// Each stage's options (optional, branch point, branch name), one for each of <see cref="StageDefinitionIdsInOrder"/>
    /// in the same order; leave out for plain stages.
    /// </summary>
    public List<PathwayTemplateStageOptionsRequest>? StageOptions { get; set; }
}

/// <summary>One template stage's options. The default is a plain stage, which always runs.</summary>
public sealed class PathwayTemplateStageOptionsRequest
{
    /// <summary>Staff may skip it, with a reason, while it's the current stage.</summary>
    public bool IsOptional { get; set; }

    /// <summary>A branch point: finishing it, the clinician chooses one of the branches that follow.</summary>
    public bool ChoosesBranch { get; set; }

    /// <summary>The branch it belongs to (at most 50 characters), after a branch point; null for a stage that always runs.</summary>
    public string? Branch { get; set; }
}
