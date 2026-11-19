using MedApp.Domain.Teams;

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
    /// The team it belongs to. Moving a template to another affects only pathways created afterwards: each
    /// pathway keeps its own copy (<see cref="PatientPathway.TeamId"/>).
    /// </summary>
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    /// <summary>
    /// How long its patients' records are kept before review (Standard: the adult or child rule). Copied onto each
    /// pathway made from it; a longer class raises its existing pathways' too, a shorter one never lowers them
    /// (<see cref="PatientPathway.RetentionClass"/>).
    /// </summary>
    public RetentionClass RetentionClass { get; set; }

    public List<PathwayTemplateMap> Maps { get; set; } = [];

    /// <summary>
    /// How its stages follow each other (MC8), by order: a chain for a template written as a list. Stored as JSON;
    /// replaced, never changed in place.
    /// </summary>
    public List<PathwayTemplateLink> Links { get; set; } = [];

    /// <summary>The branches staff may add to its pathways part-way (MC10), in the template's order. Stored as JSON.</summary>
    public List<PathwayTemplateBranch> AddableBranches { get; set; } = [];
}
