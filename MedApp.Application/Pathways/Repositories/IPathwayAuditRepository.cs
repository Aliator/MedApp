using MedApp.Application.Pathways.Common;
using MedApp.Application.Common.Paging;
using MedApp.Application.Pathways.Audit.Queries.SearchPathwayAudit;
using MedApp.Application.Pathways.Timeline.Common;
using MedApp.Application.Pathways.Timeline.Queries.GetPatientTimeline;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Repositories;

/// <summary>The pathway audit log, read-only: entries are written with the change they record.</summary>
public interface IPathwayAuditRepository
{
    /// <summary>One page of audit entries matching the query (see <c>PathwayAuditSearch</c>).</summary>
    Task<PagedResult<PatientPathwayAuditEntry>> SearchAsync(SearchPathwayAuditQuery query, CancellationToken ct);

    /// <summary>
    /// A page of a patient's timeline and one entry more, if there is one (see <c>PatientTimelineSearch</c>), each
    /// with its pathway's title now.
    /// </summary>
    Task<IReadOnlyList<PatientTimelineRow>> GetPatientTimelineAsync(GetPatientTimelineQuery query, CancellationToken ct);

    /// <summary>
    /// These versions of this patient's pathway notes, and the one before each (for an edit's old text), read-only: the
    /// text of the notes the admin's timeline shows. Only the patient's own are read. Versions outlive their pathway,
    /// so a deleted pathway's notes are found too. A few more may come back (any of the numbers asked for, for any of
    /// the notes); look each one up by its note and version.
    /// </summary>
    Task<IReadOnlyList<PatientPathwayNoteVersion>> GetNoteVersionsAsync(
        Guid patientId,
        IReadOnlyCollection<NoteVersionKey> versions,
        CancellationToken ct);

    /// <summary>The entries recording the deletion of a patient's pathways.</summary>
    Task<IReadOnlyList<PatientPathwayAuditEntry>> GetDeletionsAsync(Guid patientId, CancellationToken ct);
}
