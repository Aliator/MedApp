namespace MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Requests;

/// <summary>Edits a template. Only the fields that are set change. Existing pathways keep their copy of the stages.</summary>
public sealed class UpdatePathwayTemplateRequest
{
    public string? Name { get; set; }

    /// <summary>Moves it to another specialty (not an archived one). Only pathways created afterwards are in the new one.</summary>
    public Guid? SpecialtyId { get; set; }

    /// <summary>The template's stages in order; the same definition may appear more than once.</summary>
    public List<Guid>? StageDefinitionIdsInOrder { get; set; }
}
