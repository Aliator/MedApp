using MedApp.Application.Procedures.Insights.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>Runs the insights aggregations (<see cref="ProcedureInsightsQueries"/>) in SQL, read-only.</summary>
public sealed class ProcedureInsightsRepository(MedAppDbContext context) : IProcedureInsightsRepository
{
    private IQueryable<PatientProcedure> Procedures(Guid? templateId) =>
        context.PatientProcedures.AsNoTracking().ForTemplate(templateId);

    private IQueryable<PatientProcedureAuditEntry> Audit(Guid? templateId) =>
        context.PatientProcedureAuditEntries.AsNoTracking().ForTemplate(context.PatientProcedures, templateId);

    public async Task<IReadOnlyList<StatusCount>> GetStatusCountsAsync(Guid? templateId, DateTime nowUtc, CancellationToken ct) =>
        await Procedures(templateId).StatusCounts(nowUtc).ToListAsync(ct);

    public async Task<IReadOnlyList<StatusCount>> GetStatusCountsAtAsync(Guid? templateId, DateTime atUtc, CancellationToken ct) =>
        await Procedures(templateId).StatusCountsAt(context.PatientProcedureAuditEntries.AsNoTracking(), atUtc).ToListAsync(ct);

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
        var overdue = Procedures(templateId).Overdue(nowUtc);
        var byAssignee = await overdue.CountByAssignee().ToListAsync(ct);
        var nobody = await overdue.CountAsync(p => !p.Assignments.Any(), ct);
        return (byAssignee, nobody);
    }

    public async Task<IReadOnlyList<PriorityCount>> CountOverdueByPriorityAsync(Guid? templateId, DateTime nowUtc, CancellationToken ct) =>
        await Procedures(templateId).Overdue(nowUtc).CountByPriority().ToListAsync(ct);

    public async Task<IReadOnlyList<StageDurationRow>> GetStageDurationsAsync(Guid? templateId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
        await Procedures(templateId)
            .StageDurations(
                context.PatientProcedureStages.AsNoTracking(),
                context.PatientProcedureAuditEntries.AsNoTracking(),
                fromUtc,
                toUtc)
            .ToListAsync(ct);
}
