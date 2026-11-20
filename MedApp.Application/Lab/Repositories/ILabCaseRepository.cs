using MedApp.Application.Common.Paging;
using MedApp.Application.Common.Teams;
using MedApp.Application.Lab.Cases.Queries;
using MedApp.Application.Lab.Common;
using MedApp.Domain.Lab;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Results;

namespace MedApp.Application.Lab.Repositories;

/// <summary>
/// Lab cases (MC15). Shares the request's DbContext with the specimen and pathway repositories, so a case's change, its
/// samples', a result and a pathway's change are saved together: <see cref="Track"/> them, then save once (here, or
/// through the pathway repository's <c>SaveAsync(PathwayChange)</c>).
/// </summary>
public interface ILabCaseRepository
{
    /// <summary>The case with its steps, test, patient, requesting team, samples and pathway's stages, read only; or null.</summary>
    Task<LabCase?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>The same, tracked for changes.</summary>
    Task<LabCase?> GetForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>A lab test with its steps (tracked, as a case may take it), or null.</summary>
    Task<LabTest?> GetTestAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// The open case of each of these stages (MC16), tracked, with its steps and test; and, for a stage that took a
    /// sample with a test, the stage's open cases for that test (MC15's joining).
    /// </summary>
    Task<IReadOnlyList<LabCase>> GetOpenForStagesAsync(IReadOnlyCollection<Guid> stageIds, CancellationToken ct);

    /// <summary>A page of cases the viewer sees, in the view's order (narrowed in SQL; clock views sorted in memory).</summary>
    Task<PagedResult<LabCase>> SearchAsync(SearchLabCasesQuery query, TeamScope scope, Guid? labTeamId, DateTime nowUtc, CancellationToken ct);

    /// <summary>
    /// The open cases the viewer sees, read only, with what their clock and next step need (the test and its steps, the
    /// case's steps, a hospital request), never the patient: for the home page's counts.
    /// </summary>
    Task<IReadOnlyList<LabCase>> GetOpenAsync(TeamScope scope, Guid? labTeamId, CancellationToken ct);

    /// <summary>Adds a change's new case (when <paramref name="isNew"/>), its new steps and audit entries, unsaved.</summary>
    void Track(LabCaseChange change, bool isNew);

    /// <summary>Adds a result a report made and other audit entries, unsaved.</summary>
    void Track(Result? result, IEnumerable<PatientPathwayAuditEntry> audit);

    /// <summary>Saves everything tracked in one <c>SaveChangesAsync</c>; a conflict is a 409.</summary>
    Task SaveAsync(CancellationToken ct);

    /// <summary>Whether a patient or pathway has lab cases (it can't be deleted then).</summary>
    Task<bool> AnyForPatientAsync(Guid patientId, CancellationToken ct);

    Task<bool> AnyForPathwayAsync(Guid pathwayId, CancellationToken ct);
}
