using MedApp.Application.Common.Paging;
using MedApp.Application.Procedures.Audit.Queries.SearchProcedureAudit;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Application.Procedures.Timeline.Common;
using MedApp.Application.Procedures.Timeline.Queries.GetPatientTimeline;
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

    public async Task<IReadOnlyList<PatientTimelineRow>> GetPatientTimelineAsync(GetPatientTimelineQuery query, CancellationToken ct) =>
        await PatientTimelineSearch
            .Page(context.PatientProcedureAuditEntries.AsNoTracking(), context.PatientProcedures.AsNoTracking(), query)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PatientProcedureNoteVersion>> GetNoteVersionsAsync(
        Guid patientId,
        IReadOnlyCollection<NoteVersionKey> versions,
        CancellationToken ct)
    {
        var noteIds = versions.Select(v => v.NoteId).Distinct().ToList();
        // Each version asked for, and the one before it (an edit shows its old text too).
        var numbers = versions.SelectMany(v => new[] { v.Version, v.Version - 1 }).Distinct().ToList();
        return await context.PatientProcedureNoteVersions
            .AsNoTracking()
            .Where(v => v.PatientId == patientId && noteIds.Contains(v.NoteId) && numbers.Contains(v.Version))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PatientProcedureAuditEntry>> GetDeletionsAsync(Guid patientId, CancellationToken ct) =>
        await context.PatientProcedureAuditEntries
            .AsNoTracking()
            .Where(e => e.PatientId == patientId && e.Action == PatientProcedureAuditAction.Deleted)
            .ToListAsync(ct);
}
