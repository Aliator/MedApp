using MedApp.Application.Common.Paging;
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

    /// <summary>A procedure with its patient, stages and assignments, read-only.</summary>
    Task<PatientProcedure?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>A procedure with its patient, stages and assignments, tracked so <see cref="SaveAsync"/> saves changes to it.</summary>
    Task<PatientProcedure?> GetForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>Saves changes to procedures loaded with <see cref="GetForUpdateAsync"/>, with their audit entries.</summary>
    Task SaveAsync(IEnumerable<PatientProcedureAuditEntry> audit, CancellationToken ct);

    /// <summary>Deletes a procedure loaded with <see cref="GetForUpdateAsync"/>, keeping its audit entries.</summary>
    Task DeleteAsync(PatientProcedure procedure, IEnumerable<PatientProcedureAuditEntry> audit, CancellationToken ct);

    /// <summary>One page of procedures matching the query (see <c>ProcedureSearch</c>), with patients, stages and assignments.</summary>
    Task<PagedResult<PatientProcedure>> SearchAsync(SearchPatientProceduresQuery query, DateTime nowUtc, CancellationToken ct);
}
