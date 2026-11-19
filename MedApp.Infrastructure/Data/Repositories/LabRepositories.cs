using MedApp.Application.Common.Paging;
using MedApp.Application.Common.Teams;
using MedApp.Application.Lab.Cases.Queries;
using MedApp.Application.Lab.Common;
using MedApp.Application.Lab.Repositories;
using MedApp.Domain.Lab;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Results;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>The lab's test catalogue (MC13).</summary>
public sealed class LabTestRepository(MedAppDbContext context) : ILabTestRepository
{
    public async Task<IReadOnlyList<LabTest>> GetAllAsync(bool includeArchived, CancellationToken ct) =>
        await context.LabTests
            .AsNoTracking()
            .Include(t => t.Steps)
            .Where(t => includeArchived || !t.IsArchived)
            .OrderBy(t => t.Name).ThenBy(t => t.Id)
            .ToListAsync(ct);

    public async Task<LabTest?> GetAsync(Guid id, CancellationToken ct) =>
        await context.LabTests.Include(t => t.Steps).FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<bool> NameExistsAsync(string name, Guid? exceptId, CancellationToken ct) =>
        await context.LabTests.AnyAsync(t => t.Name == name && t.Id != exceptId, ct);

    public async Task<IReadOnlySet<Guid>> GetInUseAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var list = ids.ToList();
        var byCases = await context.LabCases
            .Where(c => c.LabTestId != null && list.Contains(c.LabTestId.Value))
            .Select(c => c.LabTestId!.Value)
            .Distinct()
            .ToListAsync(ct);
        var bySamples = await context.Specimens
            .Where(s => s.LabTestId != null && list.Contains(s.LabTestId.Value))
            .Select(s => s.LabTestId!.Value)
            .Distinct()
            .ToListAsync(ct);
        return byCases.Concat(bySamples).ToHashSet();
    }

    public async Task AddAsync(LabTest test, CancellationToken ct)
    {
        context.LabTests.Add(test);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(LabTest test, IEnumerable<LabTestStep> newSteps, IEnumerable<LabTestStep> removedSteps, CancellationToken ct)
    {
        // New steps' keys are set, so they're added explicitly (EF would take them for existing rows).
        foreach (var step in newSteps)
            if (context.Entry(step).State != EntityState.Added)
                context.LabTestSteps.Add(step);
        foreach (var step in removedSteps)
            context.LabTestSteps.Remove(step);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct) =>
        await context.LabTests.Where(t => t.Id == id).ExecuteDeleteAsync(ct);
}

/// <summary>
/// Lab cases (MC15). Shares the request's DbContext with the specimen and pathway repositories, so what's tracked here
/// is saved with them in one <c>SaveChangesAsync</c>.
/// </summary>
public sealed class LabCaseRepository(MedAppDbContext context) : ILabCaseRepository
{
    public async Task<LabCase?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await WithDetails(context.LabCases.AsNoTracking()).FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<LabCase?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        await WithDetails(context.LabCases).FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<LabTest?> GetTestAsync(Guid id, CancellationToken ct) =>
        await context.LabTests.Include(t => t.Steps).FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<LabCase>> GetOpenForStagesAsync(IReadOnlyCollection<Guid> stageIds, CancellationToken ct)
    {
        var list = stageIds.ToList();
        return await context.LabCases
            .Include(c => c.Steps)
            .Include(c => c.LabTest!).ThenInclude(t => t.Steps)
            .Where(c => c.Status == LabCaseStatus.Open && c.StageId != null && list.Contains(c.StageId.Value))
            .AsSplitQuery()
            .ToListAsync(ct);
    }

    public async Task<PagedResult<LabCase>> SearchAsync(
        SearchLabCasesQuery query, TeamScope scope, Guid? labTeamId, DateTime nowUtc, CancellationToken ct)
    {
        var filtered = context.LabCases.AsNoTracking()
            .VisibleCases(scope, context.PathwayAccess, labTeamId)
            .Filter(query);

        // The open views and the due column follow each case's clock and next step: ordered in memory (open cases).
        if (query.NeedsMemory)
        {
            var arranged = LabCaseSearch.Arrange(await Summaries(filtered).ToListAsync(ct), query, nowUtc);
            return new PagedResult<LabCase>(
                arranged.Skip(Paging.Skip(query.Page, query.PageSize)).Take(query.PageSize).ToList(), arranged.Count);
        }

        var totalCount = await filtered.CountAsync(ct);
        var page = await Summaries(filtered
                .Sort(query.ParsedView, query.Sort, query.Ascending)
                .Skip(Paging.Skip(query.Page, query.PageSize))
                .Take(query.PageSize))
            .ToListAsync(ct);
        return new PagedResult<LabCase>(page, totalCount);
    }

    public void Track(LabCaseChange change, bool isNew)
    {
        if (isNew && context.Entry(change.Case).State != EntityState.Added)
            context.LabCases.Add(change.Case);
        foreach (var step in change.NewSteps)
            if (context.Entry(step).State != EntityState.Added)
                context.LabCaseSteps.Add(step);
        context.PatientPathwayAuditEntries.AddRange(change.AuditEntries);
    }

    public void Track(Result? result, IEnumerable<PatientPathwayAuditEntry> audit)
    {
        // The result alone: its patient, pathway and specimen are already tracked.
        if (result is not null)
            context.Entry(result).State = EntityState.Added;
        context.PatientPathwayAuditEntries.AddRange(audit);
    }

    public Task SaveAsync(CancellationToken ct) =>
        context.SaveChangesOrConflictAsync(LabCaseRules.ChangedMeanwhileMessage, ct);

    public async Task<bool> AnyForPatientAsync(Guid patientId, CancellationToken ct) =>
        await context.LabCases.AnyAsync(c => c.PatientId == patientId, ct);

    public async Task<bool> AnyForPathwayAsync(Guid pathwayId, CancellationToken ct) =>
        await context.LabCases.AnyAsync(c => c.PathwayId == pathwayId, ct);

    /// <summary>What a case in a list shows: its patient, test (with steps, for its next step), clinic and steps.</summary>
    private static IQueryable<LabCase> Summaries(IQueryable<LabCase> cases) =>
        cases
            .Include(c => c.Patient)
            .Include(c => c.RequestingTeam)
            .Include(c => c.LabTest!).ThenInclude(t => t.Steps)
            .Include(c => c.Steps)
            .AsSplitQuery();

    private static IQueryable<LabCase> WithDetails(IQueryable<LabCase> cases) =>
        cases
            .Include(c => c.Patient)
            .Include(c => c.RequestingTeam)
            .Include(c => c.LabTest!).ThenInclude(t => t.Steps)
            .Include(c => c.Steps)
            .Include(c => c.Pathway!).ThenInclude(p => p.Stages)
            .Include(c => c.Specimens).ThenInclude(s => s.SpecimenType)
            .Include(c => c.Specimens).ThenInclude(s => s.Patient)
            .Include(c => c.Specimens).ThenInclude(s => s.LabTest)
            .Include(c => c.Specimens).ThenInclude(s => s.RequestingTeam)
            .AsSplitQuery();
}
