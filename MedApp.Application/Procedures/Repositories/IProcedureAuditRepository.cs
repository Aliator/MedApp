using MedApp.Application.Procedures.Common;
using MedApp.Application.Common.Paging;
using MedApp.Application.Procedures.Audit.Queries.SearchProcedureAudit;
using MedApp.Application.Procedures.Timeline.Common;
using MedApp.Application.Procedures.Timeline.Queries.GetPatientTimeline;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Repositories;

/// <summary>The procedure audit log, read-only: entries are written with the change they record.</summary>
public interface IProcedureAuditRepository
{
    /// <summary>One page of audit entries matching the query (see <c>ProcedureAuditSearch</c>).</summary>
    Task<PagedResult<PatientProcedureAuditEntry>> SearchAsync(SearchProcedureAuditQuery query, CancellationToken ct);

    /// <summary>
    /// A page of a patient's timeline and one entry more, if there is one (see <c>PatientTimelineSearch</c>), each
    /// with its procedure's title now.
    /// </summary>
    Task<IReadOnlyList<PatientTimelineRow>> GetPatientTimelineAsync(GetPatientTimelineQuery query, CancellationToken ct);

    /// <summary>
    /// These versions of this patient's procedure notes, and the one before each (for an edit's old text), read-only: the
    /// text of the notes the admin's timeline shows. Only the patient's own are read. Versions outlive their procedure,
    /// so a deleted procedure's notes are found too. A few more may come back (any of the numbers asked for, for any of
    /// the notes); look each one up by its note and version.
    /// </summary>
    Task<IReadOnlyList<PatientProcedureNoteVersion>> GetNoteVersionsAsync(
        Guid patientId,
        IReadOnlyCollection<NoteVersionKey> versions,
        CancellationToken ct);

    /// <summary>The entries recording the deletion of a patient's procedures.</summary>
    Task<IReadOnlyList<PatientProcedureAuditEntry>> GetDeletionsAsync(Guid patientId, CancellationToken ct);
}
