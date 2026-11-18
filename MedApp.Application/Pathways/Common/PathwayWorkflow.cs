using System.Text.RegularExpressions;
using MedApp.Domain.Appointments;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.Application.Pathways.Common;

/// <summary>
/// The rules of a pathway's workflow.
/// <para>
/// Statuses: a new pathway is Unassigned, and Assigned once an admin assigns someone. Starting its first stage makes
/// it InProgress, and finishing its last stage makes it InReview, from which staff mark it Completed. Any open
/// pathway can be Blocked (and later unblocked to where it was) or Cancelled, each with a reason. Completed and
/// Cancelled pathways can be Archived.
/// </para>
/// <para>
/// Stages are worked strictly in order. The current stage is the first one not finished; its notes can be edited until
/// it's finished, which locks it. An admin can unlock a finished stage to reopen just that stage, which moves an
/// InReview or Completed pathway back to InProgress.
/// </para>
/// <para>
/// A stage may involve a visit (<see cref="VisitRequirement"/>). When it becomes current, the app asks for an
/// appointment (<see cref="StageVisits"/>). A Required one can't be finished without an attended appointment, except
/// by an admin, with a reason; finishing a Suggested one without asks for confirmation in the UI. Finishing a stage
/// again after an admin unlocked it doesn't ask for a visit again.
/// </para>
/// </summary>
public static partial class PathwayWorkflow
{
    public const string StageNotStarted = "NotStarted";
    public const string StageInProgress = "InProgress";
    public const string StageCompleted = "Completed";

    /// <summary>Work still to do: stages can be worked on.</summary>
    public static readonly IReadOnlyList<PatientPathwayStatus> OpenStatuses = [Unassigned, Assigned, InProgress, InReview];

    /// <summary>Finished with, one way or another: lists hide these unless asked.</summary>
    public static readonly IReadOnlyList<PatientPathwayStatus> ClosedStatuses = [Completed, Cancelled, Archived];

    public static bool IsOpen(PatientPathwayStatus status) => OpenStatuses.Contains(status);

    public static bool IsClosed(PatientPathwayStatus status) => ClosedStatuses.Contains(status);

    /// <summary>Blocking and cancelling need a reason, for the audit log.</summary>
    public static bool RequiresReason(PatientPathwayStatus status) => status is Blocked or Cancelled;

    /// <summary>The status changes staff can make by hand from the pathway's current status.</summary>
    public static IReadOnlyList<PatientPathwayStatus> AllowedStatusChanges(PatientPathway pathway) => pathway.Status switch
    {
        Unassigned or Assigned or InProgress => [Blocked, Cancelled],
        InReview => [Completed, Blocked, Cancelled],
        Blocked => [pathway.BlockedFromStatus ?? StatusFromAssignments(pathway), Cancelled],
        Completed or Cancelled => [Archived],
        _ => []
    };

    /// <summary>Open and past its target date. Blocked pathways are waiting on something else, so never overdue.</summary>
    public static bool IsOverdue(PatientPathway pathway, DateTime nowUtc) =>
        IsOpen(pathway.Status) && pathway.DueDateUtc < nowUtc;

    /// <summary>What an unstarted pathway's status should be: Assigned when someone's assigned, otherwise Unassigned.</summary>
    public static PatientPathwayStatus StatusFromAssignments(PatientPathway pathway) =>
        pathway.Assignments.Count > 0 ? Assigned : Unassigned;

    public static bool CanEditDetails(PatientPathway pathway) => pathway.Status is not (Cancelled or Archived);

    /// <summary>
    /// Whether notes can be added to the pathway, and their authors edit them: whenever its details can be edited, so
    /// also while it's Blocked or Completed (a note after completion, a result or a follow-up call, is normal), but not
    /// once it's Cancelled or Archived, when the record is closed.
    /// </summary>
    public static bool CanChangeNotes(PatientPathway pathway) => WhyNotesCantChange(pathway) is null;

    /// <summary>Why notes can't be added or edited on the pathway now, or null if they can.</summary>
    public static string? WhyNotesCantChange(PatientPathway pathway) =>
        CanEditDetails(pathway)
            ? null
            : $"Notes can't be added or edited while the pathway is {Describe(pathway.Status)}.";

    public static bool CanChangeAssignments(PatientPathway pathway) => !IsClosed(pathway.Status);

    /// <summary>Only Cancelled and Archived pathways can be deleted, by an admin.</summary>
    public static bool CanBeDeleted(PatientPathway pathway) => pathway.Status is Cancelled or Archived;

    public static bool CanWorkOnStages(PatientPathway pathway) => IsOpen(pathway.Status);

    /// <summary>The stage being worked on: the first one not finished. Null once every stage is finished.</summary>
    public static PatientPathwayStage? CurrentStage(PatientPathway pathway) =>
        pathway.Stages.OrderBy(s => s.StageOrder).FirstOrDefault(s => !s.IsCompleted);

    /// <summary>Every stage finished and locked (none reopened by an admin).</summary>
    public static bool AllStagesFinished(PatientPathway pathway) =>
        pathway.Stages.Count > 0 && pathway.Stages.All(s => s.IsCompleted && s.IsLocked);

    /// <summary>Whether work has begun on any stage.</summary>
    public static bool HasStarted(PatientPathway pathway) =>
        pathway.Stages.Any(s => s.StartedAtUtc is not null || s.IsCompleted);

    public static string StageState(PatientPathwayStage stage) =>
        stage.IsCompleted ? StageCompleted : stage.StartedAtUtc is null ? StageNotStarted : StageInProgress;

    /// <summary>Whether a stage's notes can be edited and it can be finished now.</summary>
    public static bool CanEditStage(PatientPathway pathway, PatientPathwayStage stage) =>
        WhyStageCantBeEdited(pathway, stage) is null;

    /// <summary>Why a stage can't be edited now, or null if it can.</summary>
    public static string? WhyStageCantBeEdited(PatientPathway pathway, PatientPathwayStage stage)
    {
        if (!CanWorkOnStages(pathway))
            return $"Stages can't be worked on while the pathway is {Describe(pathway.Status)}.";

        if (stage.IsCompleted)
            return stage.IsLocked ? $"The '{stage.Name}' stage is finished and locked. An admin can unlock it." : null;

        var current = CurrentStage(pathway);
        return current?.Id == stage.Id
            ? null
            : $"Stages are worked in order: finish the '{current?.Name}' stage first.";
    }

    /// <summary>Whether an admin can unlock a stage: it's finished and locked, and the pathway isn't blocked or closed off.</summary>
    public static bool CanUnlockStage(PatientPathway pathway, PatientPathwayStage stage) =>
        stage.IsCompleted && stage.IsLocked && (IsOpen(pathway.Status) || pathway.Status == Completed);

    /// <summary>
    /// Why a stage can't be finished yet for want of its visit: it's Required, being finished for the first time, and
    /// has no Attended appointment among <paramref name="stageAppointments"/>. Null if it can be finished.
    /// </summary>
    public static string? WhyStageNeedsVisit(PatientPathwayStage stage, IEnumerable<Appointment> stageAppointments) =>
        stage.VisitRequirement == VisitRequirement.Required && FinishesWithoutVisit(stage, stageAppointments)
            ? $"The '{stage.Name}' stage needs a visit before it can be finished: book it, and mark it Attended once it's happened. An admin can finish it without one."
            : null;

    /// <summary>
    /// Whether finishing a stage now would be without the visit it involves: it's Suggested or Required, being finished
    /// for the first time, and has no Attended appointment among <paramref name="stageAppointments"/>.
    /// </summary>
    public static bool FinishesWithoutVisit(PatientPathwayStage stage, IEnumerable<Appointment> stageAppointments) =>
        stage.VisitRequirement != VisitRequirement.None
        && !stage.IsCompleted
        && !stageAppointments.Any(a => a.StageId == stage.Id && a.Status == AppointmentStatus.Attended);

    /// <summary>A status in words, e.g. "in review".</summary>
    public static string Describe(PatientPathwayStatus status) =>
        WordBoundary().Replace(status.ToString(), " $1").ToLowerInvariant();

    [GeneratedRegex("(?<=[a-z])([A-Z])")]
    private static partial Regex WordBoundary();
}
