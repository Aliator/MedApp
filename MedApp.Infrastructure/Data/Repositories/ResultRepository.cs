using MedApp.Application.Common.Paging;
using MedApp.Application.Common.Teams;
using MedApp.Application.Results.Common;
using MedApp.Application.Results.Queries.SearchResults;
using MedApp.Application.Results.Repositories;
using MedApp.Domain.Attachments;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Results;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>
/// Results. Changes are made to a tracked result (<see cref="GetForUpdateAsync"/>, with its specimen) and saved with
/// their audit entries in one <c>SaveChangesAsync</c>, which refuses a save made from a copy read before someone else's
/// (409).
/// </summary>
public sealed class ResultRepository(MedAppDbContext context) : IResultRepository
{
    public async Task<Result?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await WithDetails(context.Results.AsNoTracking()).FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<Result?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        await WithDetails(context.Results).FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<Attachment?> GetReportForUpdateAsync(Guid attachmentId, CancellationToken ct) =>
        await context.Attachments.FirstOrDefaultAsync(a => a.Id == attachmentId, ct);

    public async Task<Attachment?> GetReportAsync(Guid attachmentId, CancellationToken ct) =>
        await context.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attachmentId, ct);

    public async Task AddAsync(Result result, IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct)
    {
        // The result alone: never its patient, pathway or specimen, which may have been read without tracking.
        context.Entry(result).State = EntityState.Added;
        context.PatientPathwayAuditEntries.AddRange(audit);
        await SaveAsync(ct);
    }

    public async Task SaveAsync(IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct)
    {
        context.PatientPathwayAuditEntries.AddRange(audit);
        await SaveAsync(ct);
    }

    private Task SaveAsync(CancellationToken ct) =>
        context.SaveChangesOrConflictAsync(ResultWorkflow.ChangedMeanwhileMessage, ct);

    public async Task<IReadOnlyList<Result>> GetAwaitingForPathwayAsync(Guid pathwayId, CancellationToken ct) =>
        await context.Results
            .AsNoTracking()
            .Where(r => r.PathwayId == pathwayId
                        && (r.Status == ResultStatus.AwaitingSignOff || r.Status == ResultStatus.Seen))
            .OrderBy(r => r.ReportedAtUtc).ThenBy(r => r.Id)
            .ToListAsync(ct);

    public async Task<PagedResult<Result>> SearchAsync(
        SearchResultsQuery query, IReadOnlyCollection<Guid> signOffHolders, CancellationToken ct)
    {
        var holders = signOffHolders.ToList();
        var filtered = context.Results.AsNoTracking().Filter(query, holders, context.PathwayAccess);
        var totalCount = await filtered.CountAsync(ct);

        var page = await filtered
            .Sort(query.ParsedView, query.Sort, query.Ascending)
            .Skip(Paging.Skip(query.Page, query.PageSize))
            .Take(query.PageSize)
            .Include(r => r.Patient)
            .Include(r => r.Pathway)
            .Include(r => r.Specimen)
            .ToListAsync(ct);

        return new PagedResult<Result>(page, totalCount);
    }

    public async Task<int> CountAwaitingForAsync(Guid userId, TeamScope scope, CancellationToken ct) =>
        await context.Results.Visible(scope, context.PathwayAccess).CountAsync(r =>
            r.ResponsibleUserId == userId
            && (r.Status == ResultStatus.AwaitingSignOff || r.Status == ResultStatus.Seen), ct);

    public async Task<bool> IsReportAsync(Guid attachmentId, CancellationToken ct) =>
        await context.Results.AnyAsync(r => r.ReportAttachmentId == attachmentId && r.Status != ResultStatus.EnteredInError, ct);

    public async Task<int> CountForPathwayAsync(Guid pathwayId, CancellationToken ct) =>
        await context.Results.CountAsync(r => r.PathwayId == pathwayId, ct);

    public async Task<int> CountForPatientAsync(Guid patientId, CancellationToken ct) =>
        await context.Results.CountAsync(r => r.PatientId == patientId, ct);

    public async Task<IReadOnlyList<Result>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await context.Results.AsNoTracking().Where(r => ids.Contains(r.Id)).ToListAsync(ct);

    /// <summary>A result with its patient, pathway (and its stages) and specimen (and its type).</summary>
    private static IQueryable<Result> WithDetails(IQueryable<Result> results) =>
        results
            .Include(r => r.Patient)
            .Include(r => r.Pathway).ThenInclude(p => p!.Stages)
            .Include(r => r.Specimen).ThenInclude(s => s!.SpecimenType);
}
