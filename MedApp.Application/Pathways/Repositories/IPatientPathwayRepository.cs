using MedApp.Application.Common.Paging;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.PatientPathways.Queries.SearchPatientPathways;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Repositories;

/// <summary>
/// Pathways, with their stages and assignments. Every change saves its audit entries in the same save, so the
/// log can't miss a change.
/// </summary>
public interface IPatientPathwayRepository
{
    /// <summary>Adds a new pathway, with its stages, and its audit entries.</summary>
    Task AddAsync(PatientPathway pathway, IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct);

    /// <summary>
    /// Adds a new pathway (the change's), with its stages, the change's audit entries, and the appointments it asked
    /// for with their history entries, in one save.
    /// </summary>
    Task AddAsync(PathwayChange change, CancellationToken ct);

    /// <summary>A pathway with its patient, stages, assignments and notes, read-only.</summary>
    Task<PatientPathway?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// A pathway with its patient, stages, assignments and notes, tracked so <see cref="SaveAsync"/> saves changes to it.
    /// </summary>
    Task<PatientPathway?> GetForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>Saves changes to pathways loaded with <see cref="GetForUpdateAsync"/>, with their audit entries.</summary>
    Task SaveAsync(IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct);

    /// <summary>
    /// Saves a change to a pathway loaded with <see cref="GetForUpdateAsync"/>: its audit entries, the appointments it
    /// asked for, the history entries of those and of the appointments it changed, and the notes it added or edited with
    /// their versions (a note edited meanwhile is a 409, <c>PathwayNotes.ChangedMeanwhileMessage</c>). Appointments loaded for update
    /// with <c>IAppointmentRepository</c> in the same request share this DbContext, so they're saved by the same
    /// <c>SaveChangesAsync</c>.
    /// </summary>
    Task SaveAsync(PathwayChange change, CancellationToken ct);

    /// <summary>
    /// Deletes a pathway loaded with <see cref="GetForUpdateAsync"/>, keeping its audit entries. Its appointments are
    /// kept too, detached from it (no pathway or stage), in the same save; their history keeps the pathway.
    /// </summary>
    Task DeleteAsync(PatientPathway pathway, IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct);

    /// <summary>A patient's pathways, in any status, read-only and without their stages, assignments or patient.</summary>
    Task<IReadOnlyList<PatientPathway>> GetByPatientAsync(Guid patientId, CancellationToken ct);

    /// <summary>The titles and patients' names of these pathways (those that exist), read-only.</summary>
    Task<IReadOnlyList<PathwayLabel>> GetLabelsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>One page of pathways matching the query (see <c>PathwaySearch</c>), with patients, stages and assignments.</summary>
    Task<PagedResult<PatientPathway>> SearchAsync(SearchPatientPathwaysQuery query, DateTime nowUtc, CancellationToken ct);
}
