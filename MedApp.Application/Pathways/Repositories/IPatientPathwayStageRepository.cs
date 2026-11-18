using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Repositories;

/// <summary>How much a stage definition is used.</summary>
/// <param name="TemplateCount">Templates that list it.</param>
/// <param name="PathwayCount">Pathways with a stage made from it.</param>
public sealed record StageDefinitionUsage(int TemplateCount, int PathwayCount)
{
    public static readonly StageDefinitionUsage None = new(0, 0);

    public bool InUse => TemplateCount > 0 || PathwayCount > 0;
}

/// <summary>Stage definitions and pathway templates, which admins manage.</summary>
public interface IPatientPathwayStagesRepository
{
    Task AddStageDefinitionAsync(PatientPathwayStageDefinition definition, CancellationToken ct);
    Task<PatientPathwayStageDefinition?> GetStageDefinitionByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Stage definitions sorted by name; archived ones only if asked.</summary>
    Task<IReadOnlyList<PatientPathwayStageDefinition>> GetStageDefinitionsAsync(bool includeArchived, CancellationToken ct);

    /// <summary>The stage definitions with these IDs (each once), archived or not.</summary>
    Task<IReadOnlyList<PatientPathwayStageDefinition>> GetStageDefinitionsByIdAsync(IEnumerable<Guid> ids, CancellationToken ct);

    /// <summary>Whether another stage definition has this name, ignoring case.</summary>
    Task<bool> StageDefinitionNameExistsAsync(string name, Guid? exceptId, CancellationToken ct);

    /// <summary>How much each of these definitions is used; a definition that's unused has no entry.</summary>
    Task<IReadOnlyDictionary<Guid, StageDefinitionUsage>> GetStageDefinitionUsageAsync(IEnumerable<Guid> ids, CancellationToken ct);

    Task UpdateStageDefinitionAsync(PatientPathwayStageDefinition definition, CancellationToken ct);
    Task DeleteStageDefinitionAsync(Guid id, CancellationToken ct);

    /// <summary>Adds a template with its stage list (Maps).</summary>
    Task AddPathwayTemplateAsync(PathwayTemplate template, CancellationToken ct);

    /// <summary>A template with its stage list in order, each with its definition, read-only.</summary>
    Task<PathwayTemplate?> GetPathwayTemplateByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Templates sorted by name, with their stage lists; archived ones only if asked.</summary>
    Task<IReadOnlyList<PathwayTemplate>> GetPathwayTemplatesAsync(bool includeArchived, CancellationToken ct);

    /// <summary>The names of the templates with these IDs, archived or not. IDs with no template (deleted) are left out.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetPathwayTemplateNamesAsync(IEnumerable<Guid> ids, CancellationToken ct);

    /// <summary>Whether another template has this name, ignoring case.</summary>
    Task<bool> PathwayTemplateNameExistsAsync(string name, Guid? exceptId, CancellationToken ct);

    /// <summary>How many pathways were created from each of these templates; an unused template has no entry.</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetTemplatePathwayCountsAsync(IEnumerable<Guid> ids, CancellationToken ct);

    /// <summary>
    /// Saves a template's name and archived flag, and replaces its stage list when one is given, with the stage at
    /// <c>clockStopStageOrder</c> (if any) as its clock-stop stage.
    /// </summary>
    Task UpdatePathwayTemplateAsync(
        PathwayTemplate template,
        IReadOnlyList<Guid>? stageDefinitionIdsInOrder,
        int? clockStopStageOrder,
        CancellationToken ct);

    Task DeletePathwayTemplateAsync(Guid id, CancellationToken ct);
}
