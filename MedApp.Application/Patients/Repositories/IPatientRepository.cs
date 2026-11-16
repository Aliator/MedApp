using MedApp.Application.Common.Paging;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Queries.SearchPatients;
using MedApp.Domain.Activity;
using MedApp.Domain.Patients;

namespace MedApp.Application.Patients.Repositories;

/// <summary>
/// Patients. Every change saves its activity log entry in the same save, so the log can't miss a change. Adding or
/// updating a patient whose NHS number another patient has throws a <see cref="Common.Exceptions.ConflictException"/>
/// (the database's unique index is the last word, should two saves race).
/// </summary>
public interface IPatientRepository
{
    Task AddAsync(Patient patient, ActivityEntry activity, CancellationToken cancellationToken);
    Task UpdateAsync(Patient patient, ActivityEntry activity, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, ActivityEntry activity, CancellationToken cancellationToken);
    Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IEnumerable<Patient>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Whether a patient other than <paramref name="exceptPatientId"/> has this NHS number (ten digits).</summary>
    Task<bool> NhsNumberInUseAsync(string nhsNumber, Guid? exceptPatientId, CancellationToken cancellationToken);

    /// <summary>The patient with this NHS number (ten digits), or null.</summary>
    Task<Patient?> GetByNhsNumberAsync(string nhsNumber, CancellationToken cancellationToken);

    /// <summary>How many procedures the patient has, in any status.</summary>
    Task<int> CountProceduresAsync(Guid patientId, CancellationToken cancellationToken);

    /// <summary>How many appointments the patient has, in any status.</summary>
    Task<int> CountAppointmentsAsync(Guid patientId, CancellationToken cancellationToken);

    /// <summary>How many of the patient's procedures are under way: not completed, cancelled or archived (blocked counts).</summary>
    Task<int> CountProceduresUnderWayAsync(Guid patientId, CancellationToken cancellationToken);

    /// <summary>One page of patients matching the query's filters, in its sort order, and the total that match.</summary>
    Task<PagedResult<Patient>> SearchAsync(SearchPatientsQuery query, CancellationToken cancellationToken);

    /// <summary>The names ("First Last") of the patients with these IDs. IDs with no patient (deleted) are left out.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>
    /// Patients who might be past their retention period: not anonymised, with no procedure under way, and whose
    /// record and procedures were last changed before <paramref name="changedBeforeUtc"/>.
    /// </summary>
    Task<IReadOnlyList<RetentionCandidate>> GetRetentionCandidatesAsync(DateTime changedBeforeUtc, CancellationToken cancellationToken);
}
