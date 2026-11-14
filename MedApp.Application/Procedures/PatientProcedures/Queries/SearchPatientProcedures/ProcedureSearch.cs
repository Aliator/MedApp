using MedApp.Application.Procedures.Common;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.PatientProcedures.Queries.SearchPatientProcedures;

/// <summary>
/// The filtering and sorting behind <see cref="SearchPatientProceduresQuery"/>, written as plain LINQ so the
/// repository runs it in SQL and the unit tests run it in memory. Text matches ignore case either way.
/// </summary>
public static class ProcedureSearch
{
    public const string SortByDueDate = "dueDate";
    public const string SortByPriority = "priority";
    public const string SortByStatus = "status";
    public const string SortByTitle = "title";
    public const string SortByPatient = "patient";
    public const string SortByLastUpdated = "lastUpdated";

    public static readonly IReadOnlyList<string> SortColumns =
        [SortByDueDate, SortByPriority, SortByStatus, SortByTitle, SortByPatient, SortByLastUpdated];

    public static bool IsSortColumn(string sort) =>
        SortColumns.Contains(sort, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Procedures matching every filter given. Completed, Cancelled and Archived ones are left out unless
    /// <c>IncludeClosed</c> is set or <c>Statuses</c> names the statuses wanted.
    /// </summary>
    public static IQueryable<PatientProcedure> Filter(
        this IQueryable<PatientProcedure> procedures,
        SearchPatientProceduresQuery query,
        DateTime nowUtc)
    {
        if (Term(query.Search) is { } term)
            procedures = procedures.Where(p =>
                p.Title.ToLower().Contains(term)
                || p.TemplateName.ToLower().Contains(term)
                || (p.Patient.FirstName + " " + p.Patient.LastName).ToLower().Contains(term));

        if (query.PatientId is { } patientId)
            procedures = procedures.Where(p => p.PatientId == patientId);

        if (query.TemplateId is { } templateId)
            procedures = procedures.Where(p => p.TemplateId == templateId);

        var statuses = (query.Statuses ?? [])
            .Select(s => ProcedureEnums.TryParse<PatientProcedureStatus>(s, out var status) ? status : (PatientProcedureStatus?)null)
            .OfType<PatientProcedureStatus>()
            .Distinct()
            .ToList();
        if (statuses.Count > 0)
        {
            procedures = procedures.Where(p => statuses.Contains(p.Status));
        }
        else if (!query.IncludeClosed)
        {
            var closed = ProcedureWorkflow.ClosedStatuses.ToList();
            procedures = procedures.Where(p => !closed.Contains(p.Status));
        }

        if (ProcedureEnums.TryParse<PatientProcedurePriority>(query.Priority, out var priority))
            procedures = procedures.Where(p => p.Priority == priority);

        if (query.AssignedToUserId is { } userId)
            procedures = procedures.Where(p => p.Assignments.Any(a => a.UserId == userId));

        if (query.AssignedToMe)
        {
            var me = query.CurrentUserId;
            procedures = procedures.Where(p => p.Assignments.Any(a => a.UserId == me));
        }

        if (query.OverdueOnly)
        {
            // The same rule as ProcedureWorkflow.IsOverdue, in a form EF can translate.
            var open = ProcedureWorkflow.OpenStatuses.ToList();
            procedures = procedures.Where(p => open.Contains(p.Status) && p.DueDateUtc < nowUtc);
        }

        if (query.Started is { } started)
        {
            // The same rule as ProcedureWorkflow.HasStarted: any stage started or finished.
            procedures = started
                ? procedures.Where(p => p.Stages.Any(s => s.StartedAtUtc != null || s.IsCompleted))
                : procedures.Where(p => !p.Stages.Any(s => s.StartedAtUtc != null || s.IsCompleted));
        }

        if (query.DueFrom is { } from)
        {
            var fromUtc = DueDates.StartOfDayUtc(from);
            procedures = procedures.Where(p => p.DueDateUtc >= fromUtc);
        }

        if (query.DueTo is { } to)
        {
            var toUtc = DueDates.EndOfDayUtc(to);
            procedures = procedures.Where(p => p.DueDateUtc <= toUtc);
        }

        return procedures;
    }

    /// <summary>
    /// Sorts by due date (the default), priority, status, title, patient (last, then first name) or last update.
    /// Ties go to the soonest due, then by ID, so paging never shows a row twice or skips one.
    /// </summary>
    public static IOrderedQueryable<PatientProcedure> Sort(this IQueryable<PatientProcedure> procedures, string? sort, bool ascending)
    {
        var column = SortColumns.FirstOrDefault(c => string.Equals(c, sort, StringComparison.OrdinalIgnoreCase)) ?? SortByDueDate;

        IOrderedQueryable<PatientProcedure> ordered = (column, ascending) switch
        {
            (SortByPriority, true) => procedures.OrderBy(p => p.Priority).ThenBy(p => p.DueDateUtc),
            (SortByPriority, false) => procedures.OrderByDescending(p => p.Priority).ThenBy(p => p.DueDateUtc),
            (SortByStatus, true) => procedures.OrderBy(p => p.Status).ThenBy(p => p.DueDateUtc),
            (SortByStatus, false) => procedures.OrderByDescending(p => p.Status).ThenBy(p => p.DueDateUtc),
            (SortByTitle, true) => procedures.OrderBy(p => p.Title.ToLower()).ThenBy(p => p.DueDateUtc),
            (SortByTitle, false) => procedures.OrderByDescending(p => p.Title.ToLower()).ThenBy(p => p.DueDateUtc),
            (SortByPatient, true) => procedures.OrderBy(p => p.Patient.LastName.ToLower()).ThenBy(p => p.Patient.FirstName.ToLower()).ThenBy(p => p.DueDateUtc),
            (SortByPatient, false) => procedures.OrderByDescending(p => p.Patient.LastName.ToLower()).ThenByDescending(p => p.Patient.FirstName.ToLower()).ThenBy(p => p.DueDateUtc),
            (SortByLastUpdated, true) => procedures.OrderBy(p => p.LastUpdated),
            (SortByLastUpdated, false) => procedures.OrderByDescending(p => p.LastUpdated),
            (_, true) => procedures.OrderBy(p => p.DueDateUtc),
            (_, false) => procedures.OrderByDescending(p => p.DueDateUtc),
        };

        return ordered.ThenBy(p => p.Id);
    }

    private static string? Term(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim().ToLower();
}
