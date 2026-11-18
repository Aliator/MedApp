using MedApp.Application.Common.Paging;
using MedApp.Application.Pathways.Audit.Queries.SearchPathwayAudit;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Pathways.Timeline.Common;
using MedApp.Application.Pathways.Timeline.Queries.GetPatientTimeline;
using MedApp.Domain.Pathways.PatientPathways;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

public sealed class PathwayAuditRepository(MedAppDbContext context) : IPathwayAuditRepository
{
    public async Task<PagedResult<PatientPathwayAuditEntry>> SearchAsync(SearchPathwayAuditQuery query, CancellationToken ct)
    {
        var filtered = context.PatientPathwayAuditEntries.AsNoTracking().Filter(query);

        var totalCount = await filtered.CountAsync(ct);

        var page = await filtered
            .Sort(query.Ascending)
            .Skip(Paging.Skip(query.Page, query.PageSize))
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<PatientPathwayAuditEntry>(page, totalCount);
    }

    public async Task<IReadOnlyList<PatientTimelineRow>> GetPatientTimelineAsync(GetPatientTimelineQuery query, CancellationToken ct) =>
        await PatientTimelineSearch
            .Page(context.PatientPathwayAuditEntries.AsNoTracking(), context.PatientPathways.AsNoTracking(), query)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PatientPathwayNoteVersion>> GetNoteVersionsAsync(
        Guid patientId,
        IReadOnlyCollection<NoteVersionKey> versions,
        CancellationToken ct)
    {
        var noteIds = versions.Select(v => v.NoteId).Distinct().ToList();
        // Each version asked for, and the one before it (an edit shows its old text too).
        var numbers = versions.SelectMany(v => new[] { v.Version, v.Version - 1 }).Distinct().ToList();
        return await context.PatientPathwayNoteVersions
            .AsNoTracking()
            .Where(v => v.PatientId == patientId && noteIds.Contains(v.NoteId) && numbers.Contains(v.Version))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PatientPathwayAuditEntry>> GetDeletionsAsync(Guid patientId, CancellationToken ct) =>
        await context.PatientPathwayAuditEntries
            .AsNoTracking()
            .Where(e => e.PatientId == patientId && e.Action == PatientPathwayAuditAction.Deleted)
            .ToListAsync(ct);
}
