using System.Globalization;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;
using Action = MedApp.Domain.Pathways.PatientPathways.PatientPathwayAuditAction;

namespace MedApp.Application.Pathways.Insights.Common;

/// <summary>
/// The aggregations behind the pathway insights, written as plain LINQ (see <c>PathwaySearch</c>), so the
/// repository runs them in SQL and the unit tests run them in memory. Each ends in a GroupBy or a Count, so SQL sends
/// back totals, not rows.
/// <para>
/// A pathway's past state is rebuilt from the audit log, backwards from now: each status change is a
/// <see cref="Action.StatusChanged"/> entry whose details are <c>{"from":"…","to":"…"}</c> (<see cref="PathwayAudit"/>),
/// so its status at a moment is the "from" of its first change after it, or its status now if nothing has changed
/// since. Its target date then is the "from" of its first target date change after it (a <see cref="Action.DetailsUpdated"/>
/// entry's <c>changes.dueDate</c>), or its target date now. Only the history since the moment is read, so the work
/// follows the period, not the whole log, and pathways from before the audit log need no history at all.
/// </para>
/// </summary>
public static class PathwayInsightsQueries
{
    // What a status change's details hold for each status it can change to (camelCase JSON, as PathwayAudit writes).
    // A reason can't fake one: JSON escapes the quotes in it.
    public const string ToAssigned = "\"to\":\"Assigned\"";
    public const string ToInProgress = "\"to\":\"InProgress\"";
    public const string ToInReview = "\"to\":\"InReview\"";
    public const string ToCompleted = "\"to\":\"Completed\"";
    public const string ToCancelled = "\"to\":\"Cancelled\"";
    public const string ToBlocked = "\"to\":\"Blocked\"";
    public const string ToArchived = "\"to\":\"Archived\"";

    // And where it changed from.
    public const string FromUnassigned = "\"from\":\"Unassigned\"";
    public const string FromAssigned = "\"from\":\"Assigned\"";
    public const string FromInProgress = "\"from\":\"InProgress\"";
    public const string FromInReview = "\"from\":\"InReview\"";
    public const string FromCompleted = "\"from\":\"Completed\"";
    public const string FromCancelled = "\"from\":\"Cancelled\"";
    public const string FromBlocked = "\"from\":\"Blocked\"";
    public const string FromArchived = "\"from\":\"Archived\"";

    /// <summary>Where a details change's old target date is: a UK day, <c>yyyy-MM-dd</c> (<c>UpdatePatientPathwayHandler</c>).</summary>
    public const string DueDateFromPath = "$.changes.dueDate.from";

    /// <summary>Only pathways made from this template, when there is one.</summary>
    public static IQueryable<PatientPathway> ForTemplate(this IQueryable<PatientPathway> pathways, Guid? templateId) =>
        templateId is { } id ? pathways.Where(p => p.TemplateId == id) : pathways;

    /// <summary>Only entries about pathways made from this template, when there is one (deleted pathways then drop out).</summary>
    public static IQueryable<PatientPathwayAuditEntry> ForTemplate(
        this IQueryable<PatientPathwayAuditEntry> entries,
        IQueryable<PatientPathway> pathways,
        Guid? templateId) =>
        templateId is { } id
            ? entries.Where(e => pathways.Any(p => p.Id == e.PatientPathwayId && p.TemplateId == id))
            : entries;

    /// <summary>How many pathways have each status now, and whether they're past their target date.</summary>
    public static IQueryable<StatusCount> StatusCounts(this IQueryable<PatientPathway> pathways, DateTime nowUtc) =>
        pathways
            .GroupBy(p => new { p.Status, IsPastDue = p.DueDateUtc < nowUtc })
            .Select(g => new StatusCount(g.Key.Status, g.Key.IsPastDue, g.Count()));

    /// <summary>
    /// How many pathways had each status at a past moment, and whether they were past their target date then, rebuilt
    /// from the audit log entries since (see the class's summary). Only pathways that existed then, and still do.
    /// </summary>
    public static IQueryable<StatusCount> StatusCountsAt(
        this IQueryable<PatientPathway> pathways,
        IQueryable<PatientPathwayAuditEntry> audit,
        DateTime atUtc)
    {
        // Target dates are whole UK days, due by the end of the day: one was past at the moment if its day was before the
        // moment's day. As yyyy-MM-dd text, the days compare in order.
        var atDay = DueDates.ToUkDate(atUtc).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return pathways
            .Where(p => p.CreatedAt <= atUtc)
            .Select(p => new
            {
                Status = audit
                    .Where(e => e.PatientPathwayId == p.Id && e.Action == Action.StatusChanged && e.AtUtc > atUtc)
                    // One change can make two at the same moment: finishing the only stage left of an unstarted
                    // pathway starts it (to InProgress) and finishes it (to InReview). The one to InProgress came
                    // first, so the other is left out. (A filter, not a second sort key: sorting on the details would
                    // stop SQL Server seeking the index on PatientPathwayId and AtUtc, and it scans the table.)
                    .Where(e => !(e.Details.Contains(FromInProgress)
                                  && audit.Any(o => o.PatientPathwayId == e.PatientPathwayId
                                                    && o.Action == Action.StatusChanged
                                                    && o.AtUtc == e.AtUtc
                                                    && o.Details.Contains(ToInProgress))))
                    .OrderBy(e => e.AtUtc)
                    .Select(e =>
                        e.Details.Contains(FromCompleted) ? Completed
                        : e.Details.Contains(FromCancelled) ? Cancelled
                        : e.Details.Contains(FromArchived) ? Archived
                        : e.Details.Contains(FromBlocked) ? Blocked
                        : e.Details.Contains(FromInReview) ? InReview
                        : e.Details.Contains(FromInProgress) ? InProgress
                        : e.Details.Contains(FromAssigned) ? Assigned
                        : Unassigned)
                    .Cast<PatientPathwayStatus?>()
                    .FirstOrDefault() ?? p.Status,
                DueDayThen = audit
                    .Where(e => e.PatientPathwayId == p.Id && e.Action == Action.DetailsUpdated && e.AtUtc > atUtc)
                    .Select(e => new { e.AtUtc, Day = InsightsFunctions.JsonValue(e.Details, DueDateFromPath) })
                    .Where(x => x.Day != null)
                    .OrderBy(x => x.AtUtc)
                    .Select(x => x.Day)
                    .FirstOrDefault(),
                p.DueDateUtc,
            })
            .Select(x => new
            {
                x.Status,
                IsPastDue = x.DueDayThen != null ? string.Compare(x.DueDayThen, atDay) < 0 : x.DueDateUtc < atUtc,
            })
            .GroupBy(x => new { x.Status, x.IsPastDue })
            .Select(g => new StatusCount(g.Key.Status, g.Key.IsPastDue, g.Count()));
    }

    /// <summary>How many times pathways were marked Completed between two moments (from inclusive, to exclusive).</summary>
    public static IQueryable<PatientPathwayAuditEntry> Completions(
        this IQueryable<PatientPathwayAuditEntry> audit,
        DateTime fromUtc,
        DateTime toUtc) =>
        audit.Where(e =>
            e.Action == Action.StatusChanged
            && e.AtUtc >= fromUtc
            && e.AtUtc < toUtc
            && e.Details.Contains(ToCompleted));

    /// <summary>Pathways created between two moments (from inclusive, to exclusive), by their audit entries, which outlive deletes.</summary>
    public static IQueryable<PatientPathwayAuditEntry> Creations(
        this IQueryable<PatientPathwayAuditEntry> audit,
        DateTime fromUtc,
        DateTime toUtc) =>
        audit.Where(e => e.Action == Action.Created && e.AtUtc >= fromUtc && e.AtUtc < toUtc);

    /// <summary>How many entries fall in each UTC hour, for grouping into UK weeks (whose starts are always on the hour).</summary>
    public static IQueryable<HourCount> CountByHour(this IQueryable<PatientPathwayAuditEntry> entries) =>
        entries
            .GroupBy(e => new { e.AtUtc.Date, e.AtUtc.Hour })
            .Select(g => new HourCount(g.Key.Date, g.Key.Hour, g.Count()));

    /// <summary>Open pathways past their target date (the same rule as <see cref="PathwayWorkflow.IsOverdue"/>).</summary>
    public static IQueryable<PatientPathway> Overdue(this IQueryable<PatientPathway> pathways, DateTime nowUtc)
    {
        var open = PathwayWorkflow.OpenStatuses.ToList();
        return pathways.Where(p => open.Contains(p.Status) && p.DueDateUtc < nowUtc);
    }

    /// <summary>How many of these pathways each person is assigned.</summary>
    public static IQueryable<AssigneeCount> CountByAssignee(this IQueryable<PatientPathway> pathways) =>
        pathways
            .SelectMany(p => p.Assignments)
            .GroupBy(a => a.UserId)
            .Select(g => new AssigneeCount(g.Key, g.Count()));

    /// <summary>How many of these pathways have each priority.</summary>
    public static IQueryable<PriorityCount> CountByPriority(this IQueryable<PatientPathway> pathways) =>
        pathways
            .GroupBy(p => p.Priority)
            .Select(g => new PriorityCount(g.Key, g.Count()));

    /// <summary>
    /// The average time spent at each stage of each template, over the stages first finished between two moments.
    /// A stage is reached when the one before it is first finished (the first stage, when the pathway is created);
    /// it waits until someone starts it, then is worked on until it's first finished. "First" because an admin can
    /// unlock a finished stage and it's finished again, which moves its finish time on but not when the pathway moved
    /// on: the audit log keeps every finish, so the first is used, falling back to the stage's own time.
    /// </summary>
    public static IQueryable<StageDurationRow> StageDurations(
        this IQueryable<PatientPathway> pathways,
        IQueryable<PatientPathwayStage> stages,
        IQueryable<PatientPathwayAuditEntry> audit,
        DateTime fromUtc,
        DateTime toUtc)
    {
        var finishes = audit.Where(e => e.Action == Action.StageFinished);

        // Stages are numbered 1, 2, 3... in each pathway, so the one before is the one numbered one less.
        var timed =
            from p in pathways
            join s in stages on p.Id equals s.PatientPathwayId
            // A stage's last finish is never before its first, so this only skips stages that can't count.
            where s.IsCompleted && s.StartedAtUtc != null && s.CompletedAtUtc != null && s.CompletedAtUtc >= fromUtc
            select new
            {
                StageId = s.Id,
                p.TemplateId,
                p.TemplateName,
                s.StageOrder,
                s.StageDefinitionId,
                s.Name,
                ReachedUtc = stages
                    .Where(b => b.PatientPathwayId == s.PatientPathwayId && b.StageOrder == s.StageOrder - 1)
                    .Select(b => finishes.Where(e => e.PatientPathwayId == b.PatientPathwayId && e.StageId == b.Id).OrderBy(e => e.AtUtc).Select(e => (DateTime?)e.AtUtc).FirstOrDefault() ?? b.CompletedAtUtc)
                    .FirstOrDefault() ?? p.CreatedAt,
                StartedUtc = s.StartedAtUtc!.Value,
                FinishedUtc = finishes.Where(e => e.PatientPathwayId == s.PatientPathwayId && e.StageId == s.Id).OrderBy(e => e.AtUtc).Select(e => (DateTime?)e.AtUtc).FirstOrDefault() ?? s.CompletedAtUtc!.Value,
            };

        return timed
            // Each stage is a row already (its ID is in it), so this changes nothing but the SQL: it makes SQL work out
            // each stage's first finish, and the one before's, once, in a derived table, rather than copying those
            // subqueries into everywhere they're used below.
            .Distinct()
            .Where(x => x.FinishedUtc >= fromUtc && x.FinishedUtc < toUtc)
            .Select(x => new
            {
                x.TemplateId,
                x.TemplateName,
                x.StageOrder,
                x.StageDefinitionId,
                x.Name,
                // A stage started before the one before it was first finished (only possible when that finish isn't in
                // the audit log, for pathways from before it) didn't wait at all.
                Waiting = InsightsFunctions.SecondsBetween(x.ReachedUtc, x.StartedUtc),
                Working = InsightsFunctions.SecondsBetween(x.StartedUtc, x.FinishedUtc),
            })
            .GroupBy(x => new { x.TemplateId, x.StageOrder, x.StageDefinitionId })
            .Select(g => new StageDurationRow(
                g.Key.TemplateId,
                g.Max(x => x.TemplateName)!,
                g.Key.StageOrder,
                g.Key.StageDefinitionId,
                g.Max(x => x.Name)!,
                g.Count(),
                g.Average(x => (double)(x.Waiting < 0 ? 0 : x.Waiting)),
                g.Average(x => (double)(x.Working < 0 ? 0 : x.Working))));
    }
}
