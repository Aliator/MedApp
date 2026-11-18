namespace MedApp.Domain.Pathways.PatientPathways;

/// <summary>
/// A standard pathway: an ordered list of stages that staff create a patient's pathway from. Only admins edit
/// templates; a pathway copies its template's stages when it's created, so later edits don't change it.
/// </summary>
public sealed class PathwayTemplate
{
    public Guid Id { get; init; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Archived templates can't be used for new pathways. A template in use is archived, never deleted.</summary>
    public bool IsArchived { get; set; }

    public List<PathwayTemplateMap> Maps { get; set; } = [];
}
