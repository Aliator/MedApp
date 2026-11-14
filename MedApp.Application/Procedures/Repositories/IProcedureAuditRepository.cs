using MedApp.Application.Common.Paging;
using MedApp.Application.Procedures.Audit.Queries.SearchProcedureAudit;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Repositories;

/// <summary>The procedure audit log, read-only: entries are written with the change they record.</summary>
public interface IProcedureAuditRepository
{
    /// <summary>One page of audit entries matching the query (see <c>ProcedureAuditSearch</c>).</summary>
    Task<PagedResult<PatientProcedureAuditEntry>> SearchAsync(SearchProcedureAuditQuery query, CancellationToken ct);
}
