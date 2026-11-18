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

    public static readonly IReadOnlyList<string> SortColumns =
        [SortByDueDate, SortByPriority, SortByStatus, SortByTitle, SortByPatient, SortByLastUpdated];

    /// <summary>The most IDs a list filter (specialties, stages) may name, so a URL can't make a huge query.</summary>
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

        if (query.SpecialtyIds is { Count: > 0 } specialtyIds)
        {
            var ids = specialtyIds.Distinct().ToList();
            pathways = pathways.Where(p => ids.Contains(p.SpecialtyId));
        }

        if (query.StageDefinitionIds is { Count: > 0 } stageDefinitionIds)
            pathways = pathways.AtStage(stageDefinitionIds);

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
    /// The stage reached: pathways whose current stage (the first one by order neither finished nor skipped, as
    /// <see cref="PathwayWorkflow.CurrentStage"/> has it) comes from any of these stage definitions, so "Waiting for
    /// histology" is the Histology definition. Only pathways not yet closed: a discharged, cancelled or archived one has
    /// stopped wherever it was, so it isn't waiting at a stage. On hold counts, since it still sits at that stage.
    /// </summary>
    public static IQueryable<PatientPathway> AtStage(this IQueryable<PatientPathway> pathways, IEnumerable<Guid> stageDefinitionIds)
    {
        // Nullable, to compare with the current stage's definition, which a pathway with every stage finished hasn't got.
        var ids = stageDefinitionIds.Distinct().Select(id => (Guid?)id).ToList();
        var closed = PathwayWorkflow.ClosedStatuses.ToList();
        return pathways.Where(p =>
            !closed.Contains(p.Status)
            && ids.Contains(p.Stages
                .Where(s => !s.IsCompleted && s.SkippedAtUtc == null)
                .OrderBy(s => s.StageOrder)
                .Select(s => (Guid?)s.StageDefinitionId)
                .FirstOrDefault()));
    }

    /// <summary>
    /// Sorts by target date (the default), priority, status, title, patient (last, then first name) or last update.
    /// Ties go to the soonest due, then by ID, so paging never shows a row twice or skips one.
    /// </summary>
    public static IOrderedQueryable<PatientPathway> Sort(this IQueryable<PatientPathway> pathways, string? sort, bool ascending)
    {
        var column = SortColumns.FirstOrDefault(c => string.Equals(c, sort, StringComparison.OrdinalIgnoreCase)) ?? SortByDueDate;

        IOrderedQueryable<PatientPathway> ordered = (column, ascending) switch
        {
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
