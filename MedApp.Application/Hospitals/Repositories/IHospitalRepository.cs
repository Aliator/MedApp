using MedApp.Application.Common.Paging;
using MedApp.Application.Hospitals.Requests;
using MedApp.Domain.Activity;
using MedApp.Domain.Hospitals;
using MedApp.Domain.Lab;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Patients;
using MedApp.Domain.Specimens;

namespace MedApp.Application.Hospitals.Repositories;

/// <summary>
/// Hospitals and their requests (MC18). Shares the request's DbContext with the lab case and specimen repositories: a
/// request registered is tracked here (<see cref="TrackRegistration"/>) and saved with its lab case in one
/// <c>SaveChangesAsync</c> (the lab case repository's <c>SaveAsync</c>).
/// </summary>
public interface IHospitalRepository
{
    /// <summary>Every hospital by name; archived ones only when asked.</summary>
    Task<IReadOnlyList<Hospital>> GetAllAsync(bool includeArchived, CancellationToken ct);

    /// <summary>A hospital, tracked for changes (and for a new case to name); or null.</summary>
    Task<Hospital?> GetAsync(Guid id, CancellationToken ct);

    Task<bool> NameExistsAsync(string name, Guid? exceptId, CancellationToken ct);

    Task<bool> OdsCodeExistsAsync(string odsCode, Guid? exceptId, CancellationToken ct);

    /// <summary>Which of these hospitals have sent requests (they can't be deleted).</summary>
    Task<IReadOnlySet<Guid>> GetInUseAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    Task AddAsync(Hospital hospital, CancellationToken ct);

    /// <summary>Saves changes to a hospital read with <see cref="GetAsync"/>.</summary>
    Task UpdateAsync(Hospital hospital, CancellationToken ct);

    Task DeleteAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// The patients a request could be for: by NHS number, or last name and date of birth (either may be null), never
    /// anonymised, by name then ID. For a person to choose from: never chosen for them (H-72).
    /// </summary>
    Task<IReadOnlyList<Patient>> FindPatientMatchesAsync(string? nhsNumber, string? lastName, DateOnly? dateOfBirth, CancellationToken ct);

    /// <summary>A patient, tracked (a request names them, and their case is added with them); or null.</summary>
    Task<Patient?> GetPatientForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>Whether another patient already has this NHS number.</summary>
    Task<bool> NhsNumberInUseAsync(string nhsNumber, CancellationToken ct);

    /// <summary>A request with its hospital, patient, sample type and test, read only; or null.</summary>
    Task<HospitalRequest?> GetRequestAsync(Guid id, CancellationToken ct);

    /// <summary>The statuses of these lab cases, by ID.</summary>
    Task<IReadOnlyDictionary<Guid, LabCaseStatus>> GetCaseStatusesAsync(IReadOnlyCollection<Guid> labCaseIds, CancellationToken ct);

    /// <summary>A page of requests (narrowed, sorted and paged in SQL), with their hospital, patient and test.</summary>
    Task<PagedResult<HospitalRequest>> SearchAsync(SearchHospitalRequestsQuery query, CancellationToken ct);

    /// <summary>
    /// Adds a registered request, its sample, the new patient (if one was made, with its activity entry) and the
    /// sample's audit entries, unsaved: the lab case repository saves them with the case.
    /// </summary>
    void TrackRegistration(
        HospitalRequest request,
        Specimen sample,
        Patient? newPatient,
        ActivityEntry? patientCreated,
        IEnumerable<PatientPathwayAuditEntry> audit);
}
