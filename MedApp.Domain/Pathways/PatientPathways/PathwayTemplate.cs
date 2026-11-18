using MedApp.Domain.Specialties;

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

    /// <summary>
    /// The specialty it belongs to. Moving a template to another affects only pathways created afterwards: each
    /// pathway keeps its own copy (<see cref="PatientPathway.SpecialtyId"/>).
    /// </summary>
    public Guid SpecialtyId { get; set; }
    public Specialty Specialty { get; set; } = null!;

    /// <summary>
    /// How long its patients' records are kept before review (Standard: the adult or child rule). Copied onto each
    /// pathway made from it; changing it changes its existing pathways too (<see cref="PatientPathway.RetentionClass"/>).
    /// </summary>
    public RetentionClass RetentionClass { get; set; }

    public List<PathwayTemplateMap> Maps { get; set; } = [];
}
