using MedApp.Application.Common.Teams;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.PatientPathways.Queries.SearchPatientPathways;

/// <summary>
/// The filtering and sorting behind <see cref="SearchPatientPathwaysQuery"/>, written as plain LINQ so the
/// repository runs it in SQL and the unit tests run it in memory. Text matches ignore case either way.
/// </summary>
public static class PathwaySearch
{
    public const string SortByDueDate = "dueDate";
    public const string SortByPriority = "priority";
    public const string SortByStatus = "status";
    public const string SortByTitle = "title";
    public const string SortByPatient = "patient";
    public const string SortByLastUpdated = "lastUpdated";

    /// <summary>The referral's waiting-time clock's days waited; pathways without a clock last, either way.</summary>
    public const string SortByDaysWaited = "daysWaited";

    public static readonly IReadOnlyList<string> SortColumns =
        [SortByDueDate, SortByPriority, SortByStatus, SortByTitle, SortByPatient, SortByLastUpdated, SortByDaysWaited];

    /// <summary>A current stage my team works (MC7).</summary>
    public const string ViewOurTurn = "OurTurn";

    /// <summary>That, handed to my team and not picked up yet.</summary>
    public const string ViewNewForUs = "NewForUs";

    /// <summary>My team's pathway, with a current stage the lab's.</summary>
    public const string ViewAwaitingLab = "AwaitingLab";

    /// <summary>My team's pathway, with a current stage handed back from the lab and not picked up yet.</summary>
    public const string ViewBackFromLab = "BackFromLab";

    /// <summary>The team worklist views (My Worklist's Team view), as the light demo has them.</summary>
    public static readonly IReadOnlyList<string> Views = [ViewOurTurn, ViewNewForUs, ViewAwaitingLab, ViewBackFromLab];

    public const string ViewMessage = "Choose OurTurn, NewForUs, AwaitingLab or BackFromLab.";

    public const string TeamNotYoursMessage = "Choose one of your own teams, or all of them.";

    /// <summary>The most IDs a list filter (teams, stages) may name, so a URL can't make a huge query.</summary>
    public const int MaxIdsPerFilter = 100;

    public static bool IsSortColumn(string sort) =>
        SortColumns.Contains(sort, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Pathways matching every filter given. Completed, Cancelled and Archived ones are left out unless
    /// <c>IncludeClosed</c> is set or <c>Statuses</c> names the statuses wanted.
    /// </summary>
    public static IQueryable<PatientPathway> Filter(
        this IQueryable<PatientPathway> pathways,
        SearchPatientPathwaysQuery query,
        DateTime nowUtc)
    {
        if (Term(query.Search) is { } term)
            pathways = pathways.Where(p =>
                p.Title.ToLower().Contains(term)
                || p.TemplateName.ToLower().Contains(term)
                || (p.Patient.FirstName + " " + p.Patient.LastName).ToLower().Contains(term));

        if (Term(query.Title) is { } title)
            pathways = pathways.Where(p => p.Title.ToLower().Contains(title));

        if (Term(query.TemplateName) is { } templateName)
            pathways = pathways.Where(p => p.TemplateName.ToLower().Contains(templateName));

        if (Term(query.PatientName) is { } patientName)
            pathways = pathways.Where(p => (p.Patient.FirstName + " " + p.Patient.LastName).ToLower().Contains(patientName));

        if (query.PatientId is { } patientId)
            pathways = pathways.Where(p => p.PatientId == patientId);

        if (query.TemplateId is { } templateId)
            pathways = pathways.Where(p => p.TemplateId == templateId);

        if (query.TeamIds is { Count: > 0 } teamIds)
        {
            var ids = teamIds.Distinct().ToList();
            pathways = pathways.Where(p => ids.Contains(p.TeamId));
        }

        if (query.StageDefinitionIds is { Count: > 0 } stageDefinitionIds)
            pathways = pathways.AtStage(stageDefinitionIds);

        if (query.TeamId is { } pickedTeam)
            pathways = pathways.Where(p => p.TeamId == pickedTeam);

        if (Views.FirstOrDefault(v => string.Equals(v, query.View, StringComparison.OrdinalIgnoreCase)) is { } view)
            pathways = pathways.InView(view, query.Teams, query.LabTeamId);

        var statuses = (query.Statuses ?? [])
            .Select(s => PathwayEnums.TryParse<PatientPathwayStatus>(s, out var status) ? status : (PatientPathwayStatus?)null)
            .OfType<PatientPathwayStatus>()
            .Distinct()
            .ToList();
        if (statuses.Count > 0)
        {
            pathways = pathways.Where(p => statuses.Contains(p.Status));
        }
        else if (!query.IncludeClosed)
        {
            var closed = PathwayWorkflow.ClosedStatuses.ToList();
            pathways = pathways.Where(p => !closed.Contains(p.Status));
        }

        if (PathwayEnums.TryParse<PatientPathwayPriority>(query.Priority, out var priority))
            pathways = pathways.Where(p => p.Priority == priority);

        var priorities = (query.Priorities ?? [])
            .Select(s => PathwayEnums.TryParse<PatientPathwayPriority>(s, out var p) ? p : (PatientPathwayPriority?)null)
            .OfType<PatientPathwayPriority>()
            .Distinct()
            .ToList();
        if (priorities.Count > 0)
            pathways = pathways.Where(p => priorities.Contains(p.Priority));

        if (query.AssignedToUserId is { } userId)
            pathways = pathways.Where(p => p.Assignments.Any(a => a.UserId == userId));

        if (query.NobodyAssigned)
            pathways = pathways.Where(p => !p.Assignments.Any());

        if (query.AssignedToMe)
        {
            var me = query.CurrentUserId;
            pathways = pathways.Where(p => p.Assignments.Any(a => a.UserId == me));
        }

        if (query.OverdueOnly)
        {
            // The same rule as PathwayWorkflow.IsOverdue, in a form EF can translate.
            var open = PathwayWorkflow.OpenStatuses.ToList();
            pathways = pathways.Where(p => open.Contains(p.Status) && p.DueDateUtc < nowUtc);
        }

        if (query.BreachedOnly)
        {
            // The same rule as WaitingClock.IsBreached: the day the wait is counted to is past the clock's target.
            var today = DueDates.ToUkDate(nowUtc);
            pathways = pathways.Where(p =>
                p.Referral != null
                && (p.Referral.StoppedOn ?? p.Referral.PausedSinceOn ?? today) > p.Referral.TargetOn);
        }

        if (query.Started is { } started)
        {
            // The same rule as PathwayWorkflow.HasStarted: any stage started or finished.
            pathways = started
                ? pathways.Where(p => p.Stages.Any(s => s.StartedAtUtc != null || s.IsCompleted))
                : pathways.Where(p => !p.Stages.Any(s => s.StartedAtUtc != null || s.IsCompleted));
        }

        if (query.DueFrom is { } from)
        {
            var fromUtc = DueDates.StartOfDayUtc(from);
            pathways = pathways.Where(p => p.DueDateUtc >= fromUtc);
        }

        if (query.DueTo is { } to)
        {
            var toUtc = DueDates.EndOfDayUtc(to);
            pathways = pathways.Where(p => p.DueDateUtc <= toUtc);
        }

        return pathways;
    }

    /// <summary>
    /// The stage reached: pathways one of whose current stages (MC8: still to do, with nothing before it still to do, as
    /// <see cref="PathwayWorkflow.CurrentStages"/> has it, through the stage links in SQL; a pathway without links is a
    /// chain) comes from any of these stage definitions, so "Waiting for histology" is the Histology definition. Only
    /// pathways not yet closed: a discharged, cancelled or archived one has stopped wherever it was, so it isn't waiting
    /// at a stage. On hold counts, since it still sits at that stage.
    /// </summary>
    public static IQueryable<PatientPathway> AtStage(this IQueryable<PatientPathway> pathways, IEnumerable<Guid> stageDefinitionIds)
    {
        var ids = stageDefinitionIds.Distinct().ToList();
        var closed = PathwayWorkflow.ClosedStatuses.ToList();
        return pathways.Where(p =>
            !closed.Contains(p.Status)
            && p.Stages.Any(s =>
                ids.Contains(s.StageDefinitionId)
                && !s.IsCompleted && s.SkippedAtUtc == null
                && (p.Links.Any()
                    ? !p.Links.Any(l => l.ToStageId == s.Id
                                        && p.Stages.Any(t => t.Id == l.FromStageId && !t.IsCompleted && t.SkippedAtUtc == null))
                    : !p.Stages.Any(t => t.StageOrder < s.StageOrder && !t.IsCompleted && t.SkippedAtUtc == null))));
    }

    /// <summary>
    /// A team worklist view (MC7, the relay), for the viewer's teams, among open pathways (not on hold, whose stages
    /// aren't being worked): <see cref="ViewOurTurn"/>, <see cref="ViewNewForUs"/>, <see cref="ViewAwaitingLab"/> or
    /// <see cref="ViewBackFromLab"/>. A current stage is as in <see cref="AtStage"/>; a stage's team is its owner, else
    /// the pathway's own. <paramref name="labTeamId"/> is the lab's (none: the lab views are empty).
    /// </summary>
    public static IQueryable<PatientPathway> InView(
        this IQueryable<PatientPathway> pathways, string view, TeamScope scope, Guid? labTeamId)
    {
        var mine = scope.TeamIds.ToList();
        var lab = labTeamId ?? Guid.Empty;
        var open = PathwayWorkflow.OpenStatuses.ToList();
        pathways = pathways.Where(p => open.Contains(p.Status));

        return view switch
        {
            ViewOurTurn => pathways.Where(p => p.Stages.Any(s =>
                !s.IsCompleted && s.SkippedAtUtc == null
                && mine.Contains(s.OwnerTeamId ?? p.TeamId)
                && (p.Links.Any()
                    ? !p.Links.Any(l => l.ToStageId == s.Id
                                        && p.Stages.Any(t => t.Id == l.FromStageId && !t.IsCompleted && t.SkippedAtUtc == null))
                    : !p.Stages.Any(t => t.StageOrder < s.StageOrder && !t.IsCompleted && t.SkippedAtUtc == null)))),
            ViewNewForUs => pathways.Where(p => p.Stages.Any(s =>
                !s.IsCompleted && s.SkippedAtUtc == null
                && mine.Contains(s.OwnerTeamId ?? p.TeamId)
                && s.HandedOverFromTeamId != null && s.PickedUpAtUtc == null
                && (p.Links.Any()
                    ? !p.Links.Any(l => l.ToStageId == s.Id
                                        && p.Stages.Any(t => t.Id == l.FromStageId && !t.IsCompleted && t.SkippedAtUtc == null))
                    : !p.Stages.Any(t => t.StageOrder < s.StageOrder && !t.IsCompleted && t.SkippedAtUtc == null)))),
            ViewAwaitingLab => pathways.Where(p =>
                mine.Contains(p.TeamId) && p.TeamId != lab
                && p.Stages.Any(s =>
                    !s.IsCompleted && s.SkippedAtUtc == null
                    && s.OwnerTeamId == lab
                    && (p.Links.Any()
                    ? !p.Links.Any(l => l.ToStageId == s.Id
                                        && p.Stages.Any(t => t.Id == l.FromStageId && !t.IsCompleted && t.SkippedAtUtc == null))
                    : !p.Stages.Any(t => t.StageOrder < s.StageOrder && !t.IsCompleted && t.SkippedAtUtc == null)))),
            ViewBackFromLab => pathways.Where(p =>
                mine.Contains(p.TeamId)
                && p.Stages.Any(s =>
                    !s.IsCompleted && s.SkippedAtUtc == null
                    && s.HandedOverFromTeamId == lab && s.PickedUpAtUtc == null
                    && (p.Links.Any()
                    ? !p.Links.Any(l => l.ToStageId == s.Id
                                        && p.Stages.Any(t => t.Id == l.FromStageId && !t.IsCompleted && t.SkippedAtUtc == null))
                    : !p.Stages.Any(t => t.StageOrder < s.StageOrder && !t.IsCompleted && t.SkippedAtUtc == null)))),
            _ => pathways
        };
    }

    /// <summary>
    /// Sorts by target date (the default), priority, status, title, patient (last, then first name), last update, or
    /// days waited on the referral's clock (to <paramref name="today"/> while it runs; pathways without a referral
    /// last, either way). Ties go to the soonest due, then by ID, so paging never shows a row twice or skips one.
    /// Days waited is date arithmetic on dates staff entered (<c>DateOnly.DayNumber</c>, which EF translates), never
    /// anything clinical.
    /// </summary>
    public static IOrderedQueryable<PatientPathway> Sort(
        this IQueryable<PatientPathway> pathways, string? sort, bool ascending, DateOnly today = default)
    {
        var column = SortColumns.FirstOrDefault(c => string.Equals(c, sort, StringComparison.OrdinalIgnoreCase)) ?? SortByDueDate;

        IOrderedQueryable<PatientPathway> ordered = (column, ascending) switch
        {
            (SortByDaysWaited, true) => pathways
                .OrderBy(p => p.Referral == null ? 1 : 0)
                .ThenBy(p => p.Referral == null
                    ? 0
                    : (p.Referral.StoppedOn ?? p.Referral.PausedSinceOn ?? today).DayNumber - p.Referral.WaitStartOn.DayNumber)
                .ThenBy(p => p.DueDateUtc),
            (SortByDaysWaited, false) => pathways
                .OrderBy(p => p.Referral == null ? 1 : 0)
                .ThenByDescending(p => p.Referral == null
                    ? 0
                    : (p.Referral.StoppedOn ?? p.Referral.PausedSinceOn ?? today).DayNumber - p.Referral.WaitStartOn.DayNumber)
                .ThenBy(p => p.DueDateUtc),
            (SortByPriority, true) => pathways.OrderBy(p => p.Priority).ThenBy(p => p.DueDateUtc),
            (SortByPriority, false) => pathways.OrderByDescending(p => p.Priority).ThenBy(p => p.DueDateUtc),
            (SortByStatus, true) => pathways.OrderBy(p => p.Status).ThenBy(p => p.DueDateUtc),
            (SortByStatus, false) => pathways.OrderByDescending(p => p.Status).ThenBy(p => p.DueDateUtc),
            (SortByTitle, true) => pathways.OrderBy(p => p.Title.ToLower()).ThenBy(p => p.DueDateUtc),
            (SortByTitle, false) => pathways.OrderByDescending(p => p.Title.ToLower()).ThenBy(p => p.DueDateUtc),
            (SortByPatient, true) => pathways.OrderBy(p => p.Patient.LastName.ToLower()).ThenBy(p => p.Patient.FirstName.ToLower()).ThenBy(p => p.DueDateUtc),
            (SortByPatient, false) => pathways.OrderByDescending(p => p.Patient.LastName.ToLower()).ThenByDescending(p => p.Patient.FirstName.ToLower()).ThenBy(p => p.DueDateUtc),
            (SortByLastUpdated, true) => pathways.OrderBy(p => p.LastUpdated),
            (SortByLastUpdated, false) => pathways.OrderByDescending(p => p.LastUpdated),
            (_, true) => pathways.OrderBy(p => p.DueDateUtc),
            (_, false) => pathways.OrderByDescending(p => p.DueDateUtc),
        };

        return ordered.ThenBy(p => p.Id);
    }

    private static string? Term(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim().ToLower();
}
