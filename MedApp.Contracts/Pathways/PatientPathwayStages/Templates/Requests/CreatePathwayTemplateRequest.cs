namespace MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Requests;

public sealed class CreatePathwayTemplateRequest
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The specialty it belongs to (required; not an archived one).</summary>
    public Guid SpecialtyId { get; set; }

    /// <summary>The template's stages in order; the same definition may appear more than once.</summary>
    public IReadOnlyList<Guid> StageDefinitionIdsInOrder { get; set; } = Array.Empty<Guid>();
}
