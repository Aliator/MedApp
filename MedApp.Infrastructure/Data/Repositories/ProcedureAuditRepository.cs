using MedApp.Application.Common.Paging;
using MedApp.Application.Procedures.Audit.Queries.SearchProcedureAudit;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

public sealed class ProcedureAuditRepository(MedAppDbContext context) : IProcedureAuditRepository
{
    public async Task<PagedResult<PatientProcedureAuditEntry>> SearchAsync(SearchProcedureAuditQuery query, CancellationToken ct)
    {
        var filtered = context.PatientProcedureAuditEntries.AsNoTracking().Filter(query);

        var totalCount = await filtered.CountAsync(ct);

        var page = await filtered
            .Sort(query.Ascending)
            .Skip(Paging.Skip(query.Page, query.PageSize))
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<PatientProcedureAuditEntry>(page, totalCount);
    }
}
