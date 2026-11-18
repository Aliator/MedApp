using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Pathways.Insights.Responses;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Insights.Common;

/// <summary>Shapes the insights' totals (<see cref="InsightsData"/>) into the response the page reads.</summary>
public static class PathwayInsightsMapper
{
    private const double SecondsPerHour = 3600;

    /// <summary>Priorities as the overdue chart lists them: most urgent first.</summary>
    private static readonly PatientPathwayPriority[] PrioritiesMostUrgentFirst =
        Enum.GetValues<PatientPathwayPriority>().OrderDescending().ToArray();

    public static PathwayInsightsResponse ToResponse(
        InsightsData data,
        InsightsPeriod period,
        Guid? templateId,
        IReadOnlyDictionary<Guid, string> usernames) => new()
    {
        Weeks = period.Weeks.Count,
        TemplateId = templateId,
        From = period.Weeks[0].Monday,
        To = DueDates.ToUkDate(period.NowUtc),
        Open = Compare(data, s => !PathwayWorkflow.IsClosed(s.Status)),
        InProgress = Compare(data, s => s.Status == PatientPathwayStatus.InProgress),
        Overdue = Compare(data, s => PathwayWorkflow.IsOpen(s.Status) && s.IsPastDue),
        CompletedThisWeek = new InsightsComparisonResponse { Current = data.CompletedThisWeek, LastWeek = data.CompletedLastWeek },
        OverdueByAssignee = OverdueByAssignee(data, usernames),
        OverdueByPriority = PrioritiesMostUrgentFirst
            .Select(priority => new OverdueByPriorityResponse
            {
                Priority = priority.ToString(),
                Count = data.OverdueByPriority.Where(p => p.Priority == priority).Sum(p => p.Count),
            })
            .ToList(),
        StageTimes = StageTimes(data.StageDurations),
        Throughput = period.Weeks
            .Select(week => new WeeklyThroughputResponse
            {
                WeekStart = week.Monday,
                Created = InWeek(data.CreatedByHour, week),
                Completed = InWeek(data.CompletedByHour, week),
            })
            .ToList(),
    };

    private static InsightsComparisonResponse Compare(InsightsData data, Func<StatusCount, bool> counts) => new()
    {
        Current = data.StatusesNow.Where(counts).Sum(s => s.Count),
        LastWeek = data.StatusesLastWeek.Where(counts).Sum(s => s.Count),
    };

    /// <summary>Most first; ties by username. Nobody assigned is a row of its own, if there are any.</summary>
    private static List<OverdueByAssigneeResponse> OverdueByAssignee(InsightsData data, IReadOnlyDictionary<Guid, string> usernames)
    {
        var rows = data.OverdueByAssignee
            .Select(a => new OverdueByAssigneeResponse
            {
                UserId = a.UserId,
                Username = usernames.GetValueOrDefault(a.UserId),
                Count = a.Count,
            })
            .ToList();

        if (data.OverdueWithNobodyAssigned > 0)
            rows.Add(new OverdueByAssigneeResponse { Count = data.OverdueWithNobodyAssigned });

        return rows
            .OrderByDescending(r => r.Count)
            .ThenBy(r => r.UserId is null)
            .ThenBy(r => r.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>A template's rows as its stages in order; templates by name.</summary>
    private static List<TemplateStageTimesResponse> StageTimes(IReadOnlyList<StageDurationRow> rows) =>
        rows
            .GroupBy(r => r.TemplateId)
            .Select(g => new TemplateStageTimesResponse
            {
                TemplateId = g.Key,
                TemplateName = g.Select(r => r.TemplateName).Max(StringComparer.Ordinal) ?? string.Empty,
                Stages = g
                    .OrderBy(r => r.StageOrder)
                    .ThenBy(r => r.StageName, StringComparer.OrdinalIgnoreCase)
                    .Select(r => new StageTimeResponse
                    {
                        StageOrder = r.StageOrder,
                        StageName = r.StageName,
                        FinishedCount = r.FinishedCount,
                        AverageWaitingHours = Hours(r.AverageWaitingSeconds),
                        AverageWorkingHours = Hours(r.AverageWorkingSeconds),
                    })
                    .ToList(),
            })
            .OrderBy(t => t.TemplateName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.TemplateId)
            .ToList();

    private static double Hours(double seconds) => Math.Round(seconds / SecondsPerHour, 2);

    private static int InWeek(IReadOnlyList<HourCount> hours, InsightsWeek week) =>
        hours.Where(h => week.Contains(h.StartUtc)).Sum(h => h.Count);
}
