using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Common;

/// <summary>
/// Edits a procedure's details (title, due date, priority) and records what changed, from and to: shared by
/// <c>UpdatePatientProcedureHandler</c> and medapp-dev's demo data, so both write the same audit entry. The handler
/// checks first that the details can be edited.
/// </summary>
public static class ProcedureDetails
{
    /// <summary>
    /// Applies each value given (null leaves it as it is) that differs from the procedure's, and returns what changed,
    /// by field: <c>title</c>, <c>dueDate</c> and <c>priority</c>, each with <c>from</c> and <c>to</c>. Empty when nothing
    /// changed. The title is compared as given, so trim it first. Notes aren't details: they're added and edited one at
    /// a time (<see cref="ProcedureChange.AddNote"/>); entries written before that may still hold a <c>notes</c> change.
    /// </summary>
    public static Dictionary<string, object> Apply(
        PatientProcedure procedure,
        string? title,
        DateOnly? dueDate,
        PatientProcedurePriority? priority)
    {
        var changes = new Dictionary<string, object>();

        if (title is { } newTitle && newTitle != procedure.Title)
        {
            changes["title"] = new { from = procedure.Title, to = newTitle };
            procedure.Title = newTitle;
        }

        if (dueDate is { } newDueDate && newDueDate != DueDates.ToUkDate(procedure.DueDateUtc))
        {
            changes["dueDate"] = new { from = DueDates.ToUkDate(procedure.DueDateUtc), to = newDueDate };
            procedure.DueDateUtc = DueDates.EndOfDayUtc(newDueDate);
        }

        if (priority is { } newPriority && newPriority != procedure.Priority)
        {
            changes["priority"] = new { from = procedure.Priority.ToString(), to = newPriority.ToString() };
            procedure.Priority = newPriority;
        }

        return changes;
    }

    /// <summary>Records a details edit on its change: one DetailsUpdated audit entry with what <see cref="Apply"/> changed.</summary>
    public static void Record(ProcedureChange change, Dictionary<string, object> changes) =>
        change.Audit(PatientProcedureAuditAction.DetailsUpdated, new { changes });
}
