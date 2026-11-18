using MedApp.Application.Patients.Common;
using MedApp.Domain.Activity;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Patients.Repositories;

/// <summary>A patient's whole record, across every table (<see cref="PatientRecord"/>).</summary>
public interface IPatientRecordRepository
{
    /// <summary>
    /// The patient's record, or null if there's no such patient. Read-only, unless <paramref name="forUpdate"/>, when
    /// it's tracked so <see cref="SaveAsync"/> saves changes to any part of it.
    /// </summary>
    Task<PatientRecord?> GetAsync(Guid patientId, bool forUpdate, CancellationToken ct);

    /// <summary>Saves changes to a record loaded for update, with the activity log entry recording them, in one save.</summary>
    Task SaveAsync(ActivityEntry activity, CancellationToken ct);

    /// <summary>As <see cref="SaveAsync(ActivityEntry, CancellationToken)"/>, with new pathway audit entries too, in the same save.</summary>
    Task SaveAsync(ActivityEntry activity, IReadOnlyCollection<PatientPathwayAuditEntry> audit, CancellationToken ct);
}
