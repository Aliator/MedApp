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

    /// <summary>The entries recording the deletion of a patient's procedures.</summary>
    Task<IReadOnlyList<PatientProcedureAuditEntry>> GetDeletionsAsync(Guid patientId, CancellationToken ct);
}
