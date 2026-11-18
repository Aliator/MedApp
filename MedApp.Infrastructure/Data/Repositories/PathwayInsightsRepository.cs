using MedApp.Application.Pathways.Insights.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>Runs the insights aggregations (<see cref="PathwayInsightsQueries"/>) in SQL, read-only.</summary>
public sealed class PathwayInsightsRepository(MedAppDbContext context) : IPathwayInsightsRepository
{
    private IQueryable<PatientPathway> Pathways(Guid? templateId) =>
        context.PatientPathways.AsNoTracking().ForTemplate(templateId);

    private IQueryable<PatientPathwayAuditEntry> Audit(Guid? templateId) =>
        context.PatientPathwayAuditEntries.AsNoTracking().ForTemplate(context.PatientPathways, templateId);

    public async Task<IReadOnlyList<StatusCount>> GetStatusCountsAsync(Guid? templateId, DateTime nowUtc, CancellationToken ct) =>
        await Pathways(templateId).StatusCounts(nowUtc).ToListAsync(ct);

    public async Task<IReadOnlyList<StatusCount>> GetStatusCountsAtAsync(Guid? templateId, DateTime atUtc, CancellationToken ct) =>
        await Pathways(templateId).StatusCountsAt(context.PatientPathwayAuditEntries.AsNoTracking(), atUtc).ToListAsync(ct);

    public async Task<int> CountCompletionsAsync(Guid? templateId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
        await Audit(templateId).Completions(fromUtc, toUtc).CountAsync(ct);

    public async Task<IReadOnlyList<HourCount>> CountCompletionsByHourAsync(Guid? templateId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
        await Audit(templateId).Completions(fromUtc, toUtc).CountByHour().ToListAsync(ct);

    public async Task<IReadOnlyList<HourCount>> CountCreationsByHourAsync(Guid? templateId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
        await Audit(templateId).Creations(fromUtc, toUtc).CountByHour().ToListAsync(ct);

    public async Task<(IReadOnlyList<AssigneeCount> ByAssignee, int NobodyAssigned)> CountOverdueByAssigneeAsync(
        Guid? templateId,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var overdue = Pathways(templateId).Overdue(nowUtc);
        var byAssignee = await overdue.CountByAssignee().ToListAsync(ct);
        var nobody = await overdue.CountAsync(p => !p.Assignments.Any(), ct);
        return (byAssignee, nobody);
    }

    public async Task<IReadOnlyList<PriorityCount>> CountOverdueByPriorityAsync(Guid? templateId, DateTime nowUtc, CancellationToken ct) =>
        await Pathways(templateId).Overdue(nowUtc).CountByPriority().ToListAsync(ct);

    public async Task<IReadOnlyList<StageDurationRow>> GetStageDurationsAsync(Guid? templateId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
        await Pathways(templateId)
            .StageDurations(
                context.PatientPathwayStages.AsNoTracking(),
                context.PatientPathwayAuditEntries.AsNoTracking(),
                fromUtc,
                toUtc)
            .ToListAsync(ct);
}
