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
/// Stages are worked strictly in order. The current stage is the first one neither finished nor skipped; its notes can
/// be edited until it's finished, which locks it. An admin can unlock a finished stage to reopen just that stage, which
/// moves an InReview or Completed pathway back to InProgress.
/// </para>
/// <para>
/// Some stages can be skipped (docs/centre-phases-3-5-plan.md, decisions 113 to 117): an optional stage, by hand with a
/// reason, while it's current; and at a branch point, the stages of the branches the clinician didn't choose. A skipped
/// stage counts as done for the status, has no visit, and can be reinstated by a holder of OverrideStages, when it runs
/// again in its place. MedApp never chooses or pre-selects a branch: the clinician does (<see cref="Branches"/> lists
/// them in the template's order).
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
    /// The stage being worked on: the first one neither finished nor skipped. Null once every stage is finished or
    /// skipped.
    /// </summary>
    public static PatientPathwayStage? CurrentStage(PatientPathway pathway) =>
        pathway.Stages.OrderBy(s => s.StageOrder).FirstOrDefault(IsToDo);

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

        var current = CurrentStage(pathway);
        return current?.Id == stage.Id
            ? null
            : $"Stages are worked in order: finish the '{current?.Name}' stage first.";
    }

    /// <summary>Whether an admin can unlock a stage: it's finished and locked, and the pathway isn't blocked or closed off.</summary>
    public static bool CanUnlockStage(PatientPathway pathway, PatientPathwayStage stage) =>
        stage.IsCompleted && stage.IsLocked && (IsOpen(pathway.Status) || pathway.Status == Completed);

    /// <summary>Whether this person can skip a stage now (<see cref="WhySkipNotAllowed"/>).</summary>
    public static bool CanSkipStage(PatientPathway pathway, PatientPathwayStage stage, bool canOverrideStages) =>
        WhySkipNotAllowed(pathway, stage, canOverrideStages) is null;

    /// <summary>
    /// Skipping an optional stage whose visit is Required is finishing it without its visit by another name, so only
    /// holders of OverrideStages may, as only they may finish one without it; and, as there, with a reason (the detail).
    /// </summary>
    public static bool SkipNeedsOverride(PatientPathwayStage stage) => stage.VisitRequirement == VisitRequirement.Required;

    /// <summary>
    /// Why a stage can't be skipped by hand now, or null if it can: it must be optional and the current stage of an
    /// open pathway (a later optional stage is skipped when it's reached, so the choice is made with what's known then).
    /// One whose visit is Required needs <paramref name="canOverrideStages"/> (<see cref="SkipNeedsOverride"/>).
    /// </summary>
    public static string? WhySkipNotAllowed(PatientPathway pathway, PatientPathwayStage stage, bool canOverrideStages)
    {
        if (!stage.IsOptional)
            return $"The '{stage.Name}' stage isn't optional, so it can't be skipped.";

        if (!CanWorkOnStages(pathway))
            return $"Stages can't be skipped while the pathway is {Describe(pathway.Status)}.";

        if (stage.IsSkipped)
            return $"The '{stage.Name}' stage has already been skipped.";

        if (stage.IsCompleted)
            return $"The '{stage.Name}' stage is finished, so it can't be skipped.";

        var current = CurrentStage(pathway);
        if (current?.Id != stage.Id)
            return $"Only the current stage can be skipped: finish the '{current?.Name}' stage first.";

        return SkipNeedsOverride(stage) && !canOverrideStages
            ? $"The '{stage.Name}' stage needs a visit, so only someone who can finish a stage without its visit can skip it."
            : null;
    }

    /// <summary>
    /// Whether a holder of OverrideStages can reinstate a skipped stage: it's skipped, and the pathway is open or
    /// discharged (as for unlocking; a discharged pathway goes back to in progress).
    /// </summary>
    public static bool CanReinstateStage(PatientPathway pathway, PatientPathwayStage stage) =>
        stage.IsSkipped && (IsOpen(pathway.Status) || pathway.Status == Completed);

    /// <summary>
    /// A branch point's branches, in the template's order (by each one's first stage), each with its stages in order:
    /// the stages with a branch name after it, up to the next branch point or the end. Empty for a stage that isn't a
    /// branch point. Names are matched ignoring case, and shown as first written. None is ever chosen or pre-selected
    /// here: the clinician chooses.
    /// </summary>
    public static IReadOnlyList<PathwayBranch> Branches(PatientPathway pathway, PatientPathwayStage branchPoint)
    {
        if (!branchPoint.ChoosesBranch)
            return [];

        var branches = new List<PathwayBranch>();
        foreach (var stage in StagesAfterUntilNextBranchPoint(pathway, branchPoint))
        {
            if (string.IsNullOrWhiteSpace(stage.Branch))
                continue;

            var branch = branches.FirstOrDefault(b => SameBranch(b.Name, stage.Branch));
            if (branch is null)
                branches.Add(new PathwayBranch(stage.Branch.Trim(), [stage]));
            else
                branch.Stages.Add(stage);
        }

        return branches;
    }

    /// <summary>
    /// The stages choosing <paramref name="branch"/> at a branch point skips: those of the other branches after it (up
    /// to the next branch point) that are still to do. Stages with no branch name always run.
    /// </summary>
    public static IReadOnlyList<PatientPathwayStage> StagesSkippedByBranch(
        PatientPathway pathway, PatientPathwayStage branchPoint, string? branch) =>
        string.IsNullOrWhiteSpace(branch)
            ? []
            : Branches(pathway, branchPoint)
                .Where(b => !SameBranch(b.Name, branch))
                .SelectMany(b => b.Stages)
                .Where(IsToDo)
                .OrderBy(s => s.StageOrder)
                .ToList();

    /// <summary>
    /// Why finishing a stage can't take <paramref name="branch"/>, or null if it can: a branch point finished for the
    /// first time needs one of its branches (none is assumed); any other finish, none (a branch can be changed only by
    /// reinstating its stages).
    /// </summary>
    public static string? WhyBranchChoiceInvalid(PatientPathway pathway, PatientPathwayStage stage, string? branch)
    {
        var choosing = stage.ChoosesBranch && !stage.IsCompleted;
        if (!choosing)
        {
            if (string.IsNullOrWhiteSpace(branch))
                return null;

            return stage.ChoosesBranch
                ? $"The '{stage.Name}' stage's branch was chosen when it was first finished. To follow another, reinstate its stages."
                : $"The '{stage.Name}' stage isn't a branch point, so no branch is chosen when it's finished.";
        }

        var branches = Branches(pathway, stage);
        if (branches.Count == 0)
            return string.IsNullOrWhiteSpace(branch) ? null : $"No branches follow the '{stage.Name}' stage.";

        var names = string.Join(", ", branches.Select(b => b.Name));
        if (string.IsNullOrWhiteSpace(branch))
            return $"Choose which branch follows the '{stage.Name}' stage: {names}.";

        return branches.Any(b => SameBranch(b.Name, branch))
            ? null
            : $"'{branch.Trim()}' isn't one of the branches after the '{stage.Name}' stage: {names}.";
    }

    /// <summary>Whether two branch names are the same branch: ignoring case and the spaces around them.</summary>
    public static bool SameBranch(string? a, string? b) =>
        string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<PatientPathwayStage> StagesAfterUntilNextBranchPoint(
        PatientPathway pathway, PatientPathwayStage branchPoint) =>
        pathway.Stages
            .Where(s => s.StageOrder > branchPoint.StageOrder)
            .OrderBy(s => s.StageOrder)
            .TakeWhile(s => !s.ChoosesBranch);

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

/// <summary>One branch after a branch point: its name, as the template first wrote it, and its stages in order.</summary>
public sealed record PathwayBranch(string Name, List<PatientPathwayStage> Stages);
