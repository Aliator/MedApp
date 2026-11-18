using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Insights.Common;

/// <summary>How many pathways had a status at some moment, split by whether they were past their target date then.</summary>
public sealed record StatusCount(PatientPathwayStatus Status, bool IsPastDue, int Count);

/// <summary>How many overdue pathways someone is assigned (a pathway with two assignees counts for both).</summary>
public sealed record AssigneeCount(Guid UserId, int Count);

/// <summary>How many overdue pathways have a priority.</summary>
public sealed record PriorityCount(PatientPathwayPriority Priority, int Count);

/// <summary>How many times something happened in one UTC hour (<paramref name="Date"/> at <paramref name="Hour"/>:00).</summary>
public sealed record HourCount(DateTime Date, int Hour, int Count)
{
    public DateTime StartUtc => DateTime.SpecifyKind(Date.Date.AddHours(Hour), DateTimeKind.Utc);
}

/// <summary>
/// One stage position of a template, over the pathways that finished it: how many, and on average how long it was
/// waiting to be started once it was reached, and how long from starting it to finishing it.
/// </summary>
public sealed record StageDurationRow(
    Guid? TemplateId,
    string TemplateName,
    int StageOrder,
    Guid StageDefinitionId,
    string StageName,
    int FinishedCount,
    double AverageWaitingSeconds,
    double AverageWorkingSeconds);

/// <summary>What the pathway insights are worked out from: each part aggregated in SQL by the repository.</summary>
public sealed record InsightsData(
    IReadOnlyList<StatusCount> StatusesNow,
    IReadOnlyList<StatusCount> StatusesLastWeek,
    int CompletedThisWeek,
    int CompletedLastWeek,
    IReadOnlyList<AssigneeCount> OverdueByAssignee,
    int OverdueWithNobodyAssigned,
    IReadOnlyList<PriorityCount> OverdueByPriority,
    IReadOnlyList<HourCount> CreatedByHour,
    IReadOnlyList<HourCount> CompletedByHour,
    IReadOnlyList<StageDurationRow> StageDurations);
