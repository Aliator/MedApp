namespace MedApp.Application.Pathways.Timeline.Common;

/// <summary>
/// What a timeline entry is about, for filtering: a grouping of audit actions (see <see cref="PatientTimelineKinds"/>),
/// an appointment's history (<see cref="Appointments"/>) or a referral's (<see cref="Referrals"/>). The API takes and returns them by name; they're never
/// stored.
/// </summary>
public enum PatientTimelineKind
{
    /// <summary>A pathway was created.</summary>
    Created,

    /// <summary>A stage was started, finished or unlocked.</summary>
    Stages,

    /// <summary>A stage's notes were saved, or a note was added to a pathway or edited.</summary>
    Notes,

    /// <summary>Someone was assigned or unassigned.</summary>
    Assignments,

    /// <summary>A pathway was blocked or unblocked.</summary>
    Blocks,

    /// <summary>Any other status change (by hand or automatic).</summary>
    Status,

    /// <summary>The details were edited, including the target date.</summary>
    DueDates,

    /// <summary>
    /// The details were edited, not including the target date (title, priority, and before pathways held several notes,
    /// their one notes field).
    /// </summary>
    Details,

    /// <summary>A pathway was deleted.</summary>
    Deleted,

    /// <summary>
    /// An appointment was requested, booked, moved, attended or not, cancelled, or its notes saved (from appointments'
    /// history, not the audit log, so no audit entry is of this kind).
    /// </summary>
    Appointments,

    /// <summary>A file was attached to the patient, a pathway, a stage or a referral, or deleted.</summary>
    Files,

    /// <summary>
    /// A referral was registered, edited, triaged, withdrawn or marked entered in error (from referrals' history, not
    /// the audit log, so no audit entry is of this kind).
    /// </summary>
    Referrals,

    /// <summary>
    /// A referral's waiting-time clock was paused, resumed or stopped, or its target re-read (from referrals' history,
    /// so no audit entry is of this kind; the target date it moves is a DueDates entry).
    /// </summary>
    WaitingTime
}
