using MedApp.Application.Common.Paging;
using MedApp.Application.Procedures.PatientProcedures.Queries.SearchPatientProcedures;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>
/// Procedures. Changes are made to a tracked procedure (<see cref="GetForUpdateAsync"/>) and saved with their audit
/// entries in one <c>SaveChangesAsync</c>, so EF works out which stages and assignments changed.
/// </summary>
public sealed class PatientProcedureRepository(MedAppDbContext context) : IPatientProcedureRepository
{
    public async Task AddAsync(PatientProcedure procedure, IEnumerable<PatientProcedureAuditEntry> audit, CancellationToken ct)
    {
        context.PatientProcedures.Add(procedure);
        context.PatientProcedureAuditEntries.AddRange(audit);
        await context.SaveChangesAsync(ct);
    }

    public async Task<PatientProcedure?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await WithDetails(context.PatientProcedures.AsNoTracking())
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<PatientProcedure?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        await WithDetails(context.PatientProcedures)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task SaveAsync(IEnumerable<PatientProcedureAuditEntry> audit, CancellationToken ct)
    {
        context.PatientProcedureAuditEntries.AddRange(audit);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(PatientProcedure procedure, IEnumerable<PatientProcedureAuditEntry> audit, CancellationToken ct)
    {
        // Stages and assignments cascade; the audit entries have no foreign key, so they stay.
        context.PatientProcedures.Remove(procedure);
        context.PatientProcedureAuditEntries.AddRange(audit);
        await context.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<PatientProcedure>> SearchAsync(SearchPatientProceduresQuery query, DateTime nowUtc, CancellationToken ct)
    {
        var filtered = context.PatientProcedures.AsNoTracking().Filter(query, nowUtc);

        var totalCount = await filtered.CountAsync(ct);

        var page = await WithDetails(filtered
                .Sort(query.Sort, query.Ascending)
                .Skip(Paging.Skip(query.Page, query.PageSize))
                .Take(query.PageSize))
            .AsSplitQuery()
            .ToListAsync(ct);

        return new PagedResult<PatientProcedure>(page, totalCount);
    }

    private static IQueryable<PatientProcedure> WithDetails(IQueryable<PatientProcedure> procedures) =>
        procedures
            .Include(x => x.Patient)
            .Include(x => x.Assignments)
            .Include(x => x.Stages.OrderBy(s => s.StageOrder));
}
