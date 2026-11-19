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
/// Stages follow their links (MC8, <see cref="PathwayGraph"/>): a stage is current when it's neither finished nor skipped
/// and nothing before it on any route is still to do, so routes side by side each have one. Its notes can be edited
/// until it's finished, which locks it. A holder of UnlockStages can unlock a finished stage to reopen just that stage,
/// which moves an InReview or Completed pathway back to InProgress.
/// </para>
/// <para>
/// An optional stage can be skipped by hand, with a reason, while it's current (docs/centre-phases-3-5-plan.md,
/// decisions 113 to 117). A skipped stage counts as done for the status, has no visit, and can be reinstated by a holder
/// of UnlockStages, when it runs again in its place. A choice node (MC10b) records the clinician's outcome as it's first
/// finished, and the pathway grows by that outcome's stages (<see cref="PathwayGrowth"/>); MedApp never chooses or
/// pre-selects one (<see cref="WhyOutcomeInvalid"/>).
/// </para>
/// <para>
/// A stage may involve a visit (<see cref="VisitRequirement"/>). When it becomes current, the app asks for an
/// appointment (<see cref="StageVisits"/>). A Required one can't be finished without an attended appointment, except
/// by a holder of FinishWithoutVisit, with a reason; finishing a Suggested one without asks for confirmation in the UI. Finishing a stage
/// again after it was unlocked doesn't ask for a visit again.
/// </para>
/// </summary>
public static partial class PathwayWorkflow
{
    public const string StageNotStarted = "NotStarted";
    public const string StageInProgress = "InProgress";
    public const string StageCompleted = "Completed";
    public const string StageSkipped = "Skipped";

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

    /// <summary>
    /// The first stage being worked on (<see cref="CurrentStages"/>), for what shows only one. Null once every stage is
    /// finished or skipped.
    /// </summary>
    public static PatientPathwayStage? CurrentStage(PatientPathway pathway) => CurrentStages(pathway).FirstOrDefault();

    /// <summary>
    /// The stages being worked on (MC8): each still to do, with nothing before it on any route still to do
    /// (<see cref="PathwayGraph"/>). A chain has one; routes side by side have one each.
    /// </summary>
    public static IReadOnlyList<PatientPathwayStage> CurrentStages(PatientPathway pathway) => PathwayGraph.CurrentStages(pathway);

    /// <summary>Whether a stage is one of the current ones.</summary>
    public static bool IsCurrent(PatientPathway pathway, PatientPathwayStage stage) => PathwayGraph.IsCurrent(pathway, stage);

    /// <summary>Every stage finished and locked (none reopened by an admin), or skipped.</summary>
    public static bool AllStagesFinished(PatientPathway pathway) =>
        pathway.Stages.Count > 0 && pathway.Stages.All(s => (s.IsCompleted && s.IsLocked) || s.IsSkipped);

    /// <summary>A stage still to be worked: neither finished nor skipped.</summary>
    public static bool IsToDo(PatientPathwayStage stage) => !stage.IsCompleted && !stage.IsSkipped;

    /// <summary>Whether work has begun on any stage.</summary>
    public static bool HasStarted(PatientPathway pathway) =>
        pathway.Stages.Any(s => s.StartedAtUtc is not null || s.IsCompleted);

    public static string StageState(PatientPathwayStage stage) =>
        stage.IsCompleted ? StageCompleted
        : stage.IsSkipped ? StageSkipped
        : stage.StartedAtUtc is null ? StageNotStarted
        : StageInProgress;

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

        if (stage.IsSkipped)
            return $"The '{stage.Name}' stage was skipped. Someone who can unlock stages can reinstate it.";

        return IsCurrent(pathway, stage)
            ? null
            : $"Stages are worked in order: finish the '{FirstBefore(pathway, stage)}' stage first.";
    }

    /// <summary>The name of the stage to finish first: the first still to do before this one, else the first current one.</summary>
    private static string? FirstBefore(PatientPathway pathway, PatientPathwayStage stage) =>
        (PathwayGraph.FirstToDoBefore(pathway, stage) ?? CurrentStage(pathway))?.Name;

    /// <summary>Whether a holder of UnlockStages can unlock a stage: it's finished and locked, and the pathway isn't blocked or closed off.</summary>
    public static bool CanUnlockStage(PatientPathway pathway, PatientPathwayStage stage) =>
        stage.IsCompleted && stage.IsLocked && (IsOpen(pathway.Status) || pathway.Status == Completed);

    /// <summary>Whether this person can skip a stage now (<see cref="WhySkipNotAllowed"/>).</summary>
    public static bool CanSkipStage(PatientPathway pathway, PatientPathwayStage stage, bool canFinishWithoutVisit) =>
        WhySkipNotAllowed(pathway, stage, canFinishWithoutVisit) is null;

    /// <summary>
    /// Skipping an optional stage whose visit is Required is finishing it without its visit by another name, so only
    /// holders of FinishWithoutVisit may, as only they may finish one without it; and, as there, with a reason (the detail).
    /// </summary>
    public static bool SkipNeedsOverride(PatientPathwayStage stage) => stage.VisitRequirement == VisitRequirement.Required;

    /// <summary>
    /// Why a stage can't be skipped by hand now, or null if it can: it must be optional and the current stage of an
    /// open pathway (a later optional stage is skipped when it's reached, so the choice is made with what's known then).
    /// One whose visit is Required needs <paramref name="canFinishWithoutVisit"/> (<see cref="SkipNeedsOverride"/>).
    /// </summary>
    public static string? WhySkipNotAllowed(PatientPathway pathway, PatientPathwayStage stage, bool canFinishWithoutVisit)
    {
        if (!stage.IsOptional)
            return $"The '{stage.Name}' stage isn't optional, so it can't be skipped.";

        if (!CanWorkOnStages(pathway))
            return $"Stages can't be skipped while the pathway is {Describe(pathway.Status)}.";

        if (stage.IsSkipped)
            return $"The '{stage.Name}' stage has already been skipped.";

        if (stage.IsCompleted)
            return $"The '{stage.Name}' stage is finished, so it can't be skipped.";

        if (!IsCurrent(pathway, stage))
            return $"Only the current stage can be skipped: finish the '{FirstBefore(pathway, stage)}' stage first.";

        return SkipNeedsOverride(stage) && !canFinishWithoutVisit
            ? $"The '{stage.Name}' stage needs a visit, so only someone who can finish a stage without its visit can skip it."
            : null;
    }

    /// <summary>
    /// Whether a holder of UnlockStages can reinstate a skipped stage: it's skipped, and the pathway is open or
    /// discharged (as for unlocking; a discharged pathway goes back to in progress).
    /// </summary>
    public static bool CanReinstateStage(PatientPathway pathway, PatientPathwayStage stage) =>
        stage.IsSkipped && (IsOpen(pathway.Status) || pathway.Status == Completed);

    /// <summary>
    /// Why finishing a stage can't take <paramref name="outcome"/>, or null if it can (MC10b): a choice node finished for
    /// the first time needs one of its outcomes, recorded by the clinician (none is assumed or pre-selected); any other
    /// finish, none (a recorded outcome isn't changed by finishing the stage again). The light demo's words.
    /// </summary>
    public static string? WhyOutcomeInvalid(PatientPathway pathway, PatientPathwayStage stage, string? outcome)
    {
        var named = outcome?.Trim() ?? string.Empty;
        if (stage.ChoiceKind is not null && stage.Outcome is null)
        {
            var outcomes = PathwayGrowth.Outcomes(pathway, stage);
            if (outcomes.Count == 0 && named.Length == 0)
                return null;

            return outcomes.Any(o => PathwayGrowth.SameOutcome(o.Name, named))
                ? null
                : $"Record the outcome: {string.Join(", ", outcomes.Select(o => o.Name))}.";
        }

        return named.Length > 0
            ? "The outcome was recorded already: it isn’t changed by finishing the stage again."
            : null;
    }

    /// <summary>The outcome as the template wrote it, for one named by the clinician (ignoring case), or null.</summary>
    public static string? OutcomeName(PatientPathway pathway, PatientPathwayStage stage, string? outcome) =>
        string.IsNullOrWhiteSpace(outcome)
            ? null
            : PathwayGrowth.Outcomes(pathway, stage).Select(o => o.Name).FirstOrDefault(n => PathwayGrowth.SameOutcome(n, outcome));

    /// <summary>
    /// Why a stage can't be finished yet for want of its visit: it's Required, being finished for the first time, and
    /// has no Attended appointment among <paramref name="stageAppointments"/>. Null if it can be finished.
    /// </summary>
    public static string? WhyStageNeedsVisit(PatientPathwayStage stage, IEnumerable<Appointment> stageAppointments) =>
        stage.VisitRequirement == VisitRequirement.Required && FinishesWithoutVisit(stage, stageAppointments)
            ? $"The '{stage.Name}' stage needs a visit before it can be finished: book it, and mark it Attended once it's happened. Someone allowed to finish a stage without its visit, such as a senior clinician, can finish it without one."
            : null;

    /// <summary>
    /// Whether finishing a stage now would be without the visit it involves: it's Suggested or Required, being finished
    /// for the first time, and has no Attended appointment among <paramref name="stageAppointments"/>.
    /// </summary>
    public static bool FinishesWithoutVisit(PatientPathwayStage stage, IEnumerable<Appointment> stageAppointments) =>
        stage.VisitRequirement != VisitRequirement.None
        && !stage.IsCompleted
        && !stageAppointments.Any(a => a.StageId == stage.Id && a.Status == AppointmentStatus.Attended);

    /// <summary>
    /// A status in words, as people see it, e.g. "in progress". Three are shown by another name than the one stored:
    /// InReview is "awaiting review", Blocked is "on hold" and Completed is "discharged" (docs/glossary.md).
    /// </summary>
    public static string Describe(PatientPathwayStatus status) => status switch
    {
        PatientPathwayStatus.InReview => "awaiting review",
        PatientPathwayStatus.Blocked => "on hold",
        PatientPathwayStatus.Completed => "discharged",
        _ => WordBoundary().Replace(status.ToString(), " $1").ToLowerInvariant()
    };

    [GeneratedRegex("(?<=[a-z])([A-Z])")]
    private static partial Regex WordBoundary();
}
