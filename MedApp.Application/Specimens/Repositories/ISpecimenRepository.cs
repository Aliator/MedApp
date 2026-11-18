using MedApp.Application.Common.Paging;
using MedApp.Application.Specimens.Queries.SearchSpecimens;
using MedApp.Domain.Attachments;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Specimens;

namespace MedApp.Application.Specimens.Repositories;

/// <summary>
/// Specimens, each loaded with its patient, pathway (and its stages), type and lab, and the setup lists of types and
/// labs. Every change saves its audit entries in the same save, so the history can't miss one; a save made from a copy
/// read before someone else's is a 409 (<see cref="Common.SpecimenWorkflow.ChangedMeanwhileMessage"/>).
/// </summary>
public interface ISpecimenRepository
{
    /// <summary>A specimen with its patient, pathway (and stages), type and lab, read-only.</summary>
    Task<Specimen?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>The same, tracked, so <see cref="SaveAsync"/> saves changes to it.</summary>
    Task<Specimen?> GetForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The specimen with exactly this number (already normalised), read-only.</summary>
    Task<Specimen?> GetByNumberAsync(string number, CancellationToken ct);

    /// <summary>The next value of the <c>SpecimenNumbers</c> sequence: never the same twice, across every API copy.</summary>
    Task<long> NextNumberAsync(CancellationToken ct);

    /// <summary>Adds a new specimen and its audit entries.</summary>
    Task AddAsync(Specimen specimen, IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct);

    /// <summary>Saves changes to a specimen loaded with <see cref="GetForUpdateAsync"/>, with its audit entries.</summary>
    Task SaveAsync(IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct);

    /// <summary>
    /// Saves a reported specimen (loaded with <see cref="GetForUpdateAsync"/>), its report's attachment record, the
    /// result it makes, and their audit entries, in one save.
    /// </summary>
    Task SaveReportAsync(
        Attachment report, Domain.Results.Result result, IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct);

    /// <summary>The specimens a stage took, oldest first, with their types and labs.</summary>
    Task<IReadOnlyList<Specimen>> GetForStageAsync(Guid stageId, CancellationToken ct);

    /// <summary>A pathway's open specimens (taken, sent or received), oldest first, with their types: the last stage's gate.</summary>
    Task<IReadOnlyList<Specimen>> GetOpenForPathwayAsync(Guid pathwayId, CancellationToken ct);

    /// <summary>A page of specimens (see <see cref="SpecimenSearch"/>), with their patients, types and labs.</summary>
    Task<PagedResult<Specimen>> SearchAsync(SearchSpecimensQuery query, DateTime nowUtc, CancellationToken ct);

    /// <summary>Whether a file is some specimen's report (it can't then be deleted on its own).</summary>
    Task<bool> IsReportAsync(Guid attachmentId, CancellationToken ct);

    /// <summary>How many specimens a pathway has, in any status (it can't be deleted while it has any).</summary>
    Task<int> CountForPathwayAsync(Guid pathwayId, CancellationToken ct);

    /// <summary>Specimens with these IDs, read-only (for the timeline's full view: their free text).</summary>
    Task<IReadOnlyList<Specimen>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    // Setup lists.

    Task<IReadOnlyList<SpecimenType>> GetTypesAsync(bool includeArchived, CancellationToken ct);

    Task<SpecimenType?> GetTypeAsync(Guid id, CancellationToken ct);

    Task<bool> TypeNameExistsAsync(string name, Guid? exceptId, CancellationToken ct);

    /// <summary>Which of these types some specimen has (so they're archived, not deleted).</summary>
    Task<IReadOnlySet<Guid>> GetTypesInUseAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    Task AddTypeAsync(SpecimenType type, CancellationToken ct);

    /// <summary>Saves a type changed after <see cref="GetTypeAsync"/>.</summary>
    Task UpdateTypeAsync(SpecimenType type, CancellationToken ct);

    Task DeleteTypeAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Laboratory>> GetLaboratoriesAsync(bool includeArchived, CancellationToken ct);

    Task<Laboratory?> GetLaboratoryAsync(Guid id, CancellationToken ct);

    Task<bool> LaboratoryNameExistsAsync(string name, Guid? exceptId, CancellationToken ct);

    /// <summary>Which of these labs some specimen was sent to (so they're archived, not deleted).</summary>
    Task<IReadOnlySet<Guid>> GetLaboratoriesInUseAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    Task AddLaboratoryAsync(Laboratory laboratory, CancellationToken ct);

    /// <summary>Saves a lab changed after <see cref="GetLaboratoryAsync"/>.</summary>
    Task UpdateLaboratoryAsync(Laboratory laboratory, CancellationToken ct);

    Task DeleteLaboratoryAsync(Guid id, CancellationToken ct);
}
