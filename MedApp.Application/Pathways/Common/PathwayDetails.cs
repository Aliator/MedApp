using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Common;

/// <summary>
/// Edits a pathway's details (title, target date, priority) and records what changed, from and to: shared by
/// <c>UpdatePatientPathwayHandler</c> and medapp-dev's demo data, so both write the same audit entry. The handler
/// checks first that the details can be edited.
/// </summary>
public static class PathwayDetails
{
    /// <summary>
    /// Applies each value given (null leaves it as it is) that differs from the pathway's, and returns what changed,
    /// by field: <c>title</c>, <c>dueDate</c> and <c>priority</c>, each with <c>from</c> and <c>to</c>. Empty when nothing
    /// changed. The title is compared as given, so trim it first. Notes aren't details: they're added and edited one at
    /// a time (<see cref="PathwayChange.AddNote"/>); entries written before that may still hold a <c>notes</c> change.
    /// </summary>
    public static Dictionary<string, object> Apply(
        PatientPathway pathway,
        string? title,
        DateOnly? dueDate,
        PatientPathwayPriority? priority)
    {
        var changes = new Dictionary<string, object>();

        if (title is { } newTitle && newTitle != pathway.Title)
        {
            changes["title"] = new { from = pathway.Title, to = newTitle };
            pathway.Title = newTitle;
        }

        if (dueDate is { } newDueDate && newDueDate != DueDates.ToUkDate(pathway.DueDateUtc))
        {
            changes["dueDate"] = new { from = DueDates.ToUkDate(pathway.DueDateUtc), to = newDueDate };
            pathway.DueDateUtc = DueDates.EndOfDayUtc(newDueDate);
        }

        if (priority is { } newPriority && newPriority != pathway.Priority)
        {
            changes["priority"] = new { from = pathway.Priority.ToString(), to = newPriority.ToString() };
            pathway.Priority = newPriority;
        }

        return changes;
    }

    /// <summary>Records a details edit on its change: one DetailsUpdated audit entry with what <see cref="Apply"/> changed.</summary>
    public static void Record(PathwayChange change, Dictionary<string, object> changes) =>
        change.Audit(PatientPathwayAuditAction.DetailsUpdated, new { changes });
}
