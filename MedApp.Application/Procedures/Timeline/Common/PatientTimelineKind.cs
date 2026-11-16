namespace MedApp.Application.Procedures.Timeline.Common;

/// <summary>
/// What a timeline entry is about, for filtering: a grouping of audit actions (see <see cref="PatientTimelineKinds"/>).
/// The API takes and returns them by name; they're never stored.
/// </summary>
public enum PatientTimelineKind
{
    /// <summary>A procedure was created.</summary>
    Created,

    /// <summary>A stage was started, finished or unlocked.</summary>
    Stages,

    /// <summary>A stage's notes were saved.</summary>
    Notes,

    /// <summary>Someone was assigned or unassigned.</summary>
    Assignments,

    /// <summary>A procedure was blocked or unblocked.</summary>
    Blocks,

    /// <summary>Any other status change (by hand or automatic).</summary>
    Status,

    /// <summary>The details were edited, including the due date.</summary>
    DueDates,

    /// <summary>The details were edited, not including the due date (title, notes, priority).</summary>
    Details,

    /// <summary>A procedure was deleted.</summary>
    Deleted,

    /// <summary>A file was attached to the patient, a procedure or a stage, or deleted.</summary>
    Files
}
