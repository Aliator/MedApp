using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Insights.Common;

/// <summary>How many procedures had a status at some moment, split by whether they were past their due date then.</summary>
public sealed record StatusCount(PatientProcedureStatus Status, bool IsPastDue, int Count);

/// <summary>How many overdue procedures someone is assigned (a procedure with two assignees counts for both).</summary>
public sealed record AssigneeCount(Guid UserId, int Count);

/// <summary>How many overdue procedures have a priority.</summary>
public sealed record PriorityCount(PatientProcedurePriority Priority, int Count);

/// <summary>How many times something happened in one UTC hour (<paramref name="Date"/> at <paramref name="Hour"/>:00).</summary>
public sealed record HourCount(DateTime Date, int Hour, int Count)
{
    public DateTime StartUtc => DateTime.SpecifyKind(Date.Date.AddHours(Hour), DateTimeKind.Utc);
}

/// <summary>
/// One stage position of a template, over the procedures that finished it: how many, and on average how long it was
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

/// <summary>What the procedure insights are worked out from: each part aggregated in SQL by the repository.</summary>
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
