using MedApp.Application.Common.Paging;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.PatientProcedures.Queries.SearchPatientProcedures;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Repositories;

/// <summary>
/// Procedures, with their stages and assignments. Every change saves its audit entries in the same save, so the
/// log can't miss a change.
/// </summary>
public interface IPatientProcedureRepository
{
    /// <summary>Adds a new procedure, with its stages, and its audit entries.</summary>
    Task AddAsync(PatientProcedure procedure, IEnumerable<PatientProcedureAuditEntry> audit, CancellationToken ct);

    /// <summary>
    /// Adds a new procedure (the change's), with its stages, the change's audit entries, and the appointments it asked
    /// for with their history entries, in one save.
    /// </summary>
    Task AddAsync(ProcedureChange change, CancellationToken ct);

    /// <summary>A procedure with its patient, stages and assignments, read-only.</summary>
    Task<PatientProcedure?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>A procedure with its patient, stages and assignments, tracked so <see cref="SaveAsync"/> saves changes to it.</summary>
    Task<PatientProcedure?> GetForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>Saves changes to procedures loaded with <see cref="GetForUpdateAsync"/>, with their audit entries.</summary>
    Task SaveAsync(IEnumerable<PatientProcedureAuditEntry> audit, CancellationToken ct);

    /// <summary>
    /// Saves a change to a procedure loaded with <see cref="GetForUpdateAsync"/>: its audit entries, the appointments it
    /// asked for, and the history entries of those and of the appointments it changed. Appointments loaded for update
    /// with <c>IAppointmentRepository</c> in the same request share this DbContext, so they're saved by the same
    /// <c>SaveChangesAsync</c>.
    /// </summary>
    Task SaveAsync(ProcedureChange change, CancellationToken ct);

    /// <summary>
    /// Deletes a procedure loaded with <see cref="GetForUpdateAsync"/>, keeping its audit entries. Its appointments are
    /// kept too, detached from it (no procedure or stage), in the same save; their history keeps the procedure.
    /// </summary>
    Task DeleteAsync(PatientProcedure procedure, IEnumerable<PatientProcedureAuditEntry> audit, CancellationToken ct);

    /// <summary>A patient's procedures, in any status, read-only and without their stages, assignments or patient.</summary>
    Task<IReadOnlyList<PatientProcedure>> GetByPatientAsync(Guid patientId, CancellationToken ct);

    /// <summary>The titles and patients' names of these procedures (those that exist), read-only.</summary>
    Task<IReadOnlyList<ProcedureLabel>> GetLabelsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>One page of procedures matching the query (see <c>ProcedureSearch</c>), with patients, stages and assignments.</summary>
    Task<PagedResult<PatientProcedure>> SearchAsync(SearchPatientProceduresQuery query, DateTime nowUtc, CancellationToken ct);
}
