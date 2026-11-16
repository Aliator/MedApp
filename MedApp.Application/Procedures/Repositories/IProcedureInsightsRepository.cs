using MedApp.Application.Procedures.Insights.Common;

namespace MedApp.Application.Procedures.Repositories;

/// <summary>
/// Totals over procedures and their audit log for the admins' insights, each aggregated in SQL by the matching
/// query in <see cref="ProcedureInsightsQueries"/>. Read-only. A <c>templateId</c> limits each to one template's
/// procedures; null means every procedure.
/// </summary>
public interface IProcedureInsightsRepository
{
    /// <summary>How many procedures have each status now, split by whether they're past their due date.</summary>
    Task<IReadOnlyList<StatusCount>> GetStatusCountsAsync(Guid? templateId, DateTime nowUtc, CancellationToken ct);

    /// <summary>The same, as they were at a past moment, from the audit log.</summary>
    Task<IReadOnlyList<StatusCount>> GetStatusCountsAtAsync(Guid? templateId, DateTime atUtc, CancellationToken ct);

    /// <summary>How many times procedures were marked Completed from one moment (inclusive) to another (exclusive).</summary>
    Task<int> CountCompletionsAsync(Guid? templateId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

    /// <summary>Completions, per UTC hour, from one moment (inclusive) to another (exclusive).</summary>
    Task<IReadOnlyList<HourCount>> CountCompletionsByHourAsync(Guid? templateId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

    /// <summary>Procedures created, per UTC hour, from one moment (inclusive) to another (exclusive).</summary>
    Task<IReadOnlyList<HourCount>> CountCreationsByHourAsync(Guid? templateId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

    /// <summary>How many overdue procedures each person is assigned, and how many have nobody assigned.</summary>
    Task<(IReadOnlyList<AssigneeCount> ByAssignee, int NobodyAssigned)> CountOverdueByAssigneeAsync(
        Guid? templateId,
        DateTime nowUtc,
        CancellationToken ct);

    /// <summary>How many overdue procedures have each priority.</summary>
    Task<IReadOnlyList<PriorityCount>> CountOverdueByPriorityAsync(Guid? templateId, DateTime nowUtc, CancellationToken ct);

    /// <summary>The average time at each stage of each template, over stages first finished in the window.</summary>
    Task<IReadOnlyList<StageDurationRow>> GetStageDurationsAsync(Guid? templateId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
}
