using MedApp.Application.Pathways.Insights.Common;

namespace MedApp.Application.Pathways.Repositories;

/// <summary>
/// Totals over pathways and their audit log for the admins' insights, each aggregated in SQL by the matching
/// query in <see cref="PathwayInsightsQueries"/>. Read-only. The <see cref="InsightsScope"/> limits each to one
/// template's or one team's pathways, or both; <see cref="InsightsScope.Everything"/> means every pathway.
/// </summary>
public interface IPathwayInsightsRepository
{
    /// <summary>How many pathways have each status now, split by whether they're past their target date.</summary>
    Task<IReadOnlyList<StatusCount>> GetStatusCountsAsync(InsightsScope scope, DateTime nowUtc, CancellationToken ct);

    /// <summary>The same, as they were at a past moment, from the audit log.</summary>
    Task<IReadOnlyList<StatusCount>> GetStatusCountsAtAsync(InsightsScope scope, DateTime atUtc, CancellationToken ct);

    /// <summary>How many times pathways were marked Completed from one moment (inclusive) to another (exclusive).</summary>
    Task<int> CountCompletionsAsync(InsightsScope scope, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

    /// <summary>Completions, per UTC hour, from one moment (inclusive) to another (exclusive).</summary>
    Task<IReadOnlyList<HourCount>> CountCompletionsByHourAsync(InsightsScope scope, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

    /// <summary>Pathways created, per UTC hour, from one moment (inclusive) to another (exclusive).</summary>
    Task<IReadOnlyList<HourCount>> CountCreationsByHourAsync(InsightsScope scope, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

    /// <summary>How many overdue pathways each person is assigned, and how many have nobody assigned.</summary>
    Task<(IReadOnlyList<AssigneeCount> ByAssignee, int NobodyAssigned)> CountOverdueByAssigneeAsync(
        InsightsScope scope,
        DateTime nowUtc,
        CancellationToken ct);

    /// <summary>How many overdue pathways have each priority.</summary>
    Task<IReadOnlyList<PriorityCount>> CountOverdueByPriorityAsync(InsightsScope scope, DateTime nowUtc, CancellationToken ct);

    /// <summary>The average time at each stage of each template, over stages first finished in the window.</summary>
    Task<IReadOnlyList<StageDurationRow>> GetStageDurationsAsync(InsightsScope scope, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
}
