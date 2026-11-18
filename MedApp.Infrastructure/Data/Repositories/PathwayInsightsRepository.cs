using MedApp.Application.Pathways.Insights.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>Runs the insights aggregations (<see cref="PathwayInsightsQueries"/>) in SQL, read-only.</summary>
public sealed class PathwayInsightsRepository(MedAppDbContext context) : IPathwayInsightsRepository
{
    private IQueryable<PatientPathway> Pathways(InsightsScope scope) =>
        context.PatientPathways.AsNoTracking().InScope(scope);

    private IQueryable<PatientPathwayAuditEntry> Audit(InsightsScope scope) =>
        context.PatientPathwayAuditEntries.AsNoTracking().InScope(context.PatientPathways, scope);

    public async Task<IReadOnlyList<StatusCount>> GetStatusCountsAsync(InsightsScope scope, DateTime nowUtc, CancellationToken ct) =>
        await Pathways(scope).StatusCounts(nowUtc).ToListAsync(ct);

    public async Task<IReadOnlyList<StatusCount>> GetStatusCountsAtAsync(InsightsScope scope, DateTime atUtc, CancellationToken ct) =>
        await Pathways(scope).StatusCountsAt(context.PatientPathwayAuditEntries.AsNoTracking(), atUtc).ToListAsync(ct);

    public async Task<int> CountCompletionsAsync(InsightsScope scope, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
        await Audit(scope).Completions(fromUtc, toUtc).CountAsync(ct);

    public async Task<IReadOnlyList<HourCount>> CountCompletionsByHourAsync(InsightsScope scope, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
        await Audit(scope).Completions(fromUtc, toUtc).CountByHour().ToListAsync(ct);

    public async Task<IReadOnlyList<HourCount>> CountCreationsByHourAsync(InsightsScope scope, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
        await Audit(scope).Creations(fromUtc, toUtc).CountByHour().ToListAsync(ct);

    public async Task<(IReadOnlyList<AssigneeCount> ByAssignee, int NobodyAssigned)> CountOverdueByAssigneeAsync(
        InsightsScope scope,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var overdue = Pathways(scope).Overdue(nowUtc);
        var byAssignee = await overdue.CountByAssignee().ToListAsync(ct);
        var nobody = await overdue.CountAsync(p => !p.Assignments.Any(), ct);
        return (byAssignee, nobody);
    }

    public async Task<IReadOnlyList<PriorityCount>> CountOverdueByPriorityAsync(InsightsScope scope, DateTime nowUtc, CancellationToken ct) =>
        await Pathways(scope).Overdue(nowUtc).CountByPriority().ToListAsync(ct);

    public async Task<IReadOnlyList<StageDurationRow>> GetStageDurationsAsync(InsightsScope scope, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
        await Pathways(scope)
            .StageDurations(
                context.PatientPathwayStages.AsNoTracking(),
                context.PatientPathwayAuditEntries.AsNoTracking(),
                fromUtc,
                toUtc)
            .ToListAsync(ct);
}
