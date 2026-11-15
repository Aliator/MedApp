using System.Text.RegularExpressions;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.Application.Procedures.Common;

/// <summary>
/// The rules of a procedure's workflow.
/// <para>
/// Statuses: a new procedure is Unassigned, and Assigned once an admin assigns someone. Starting its first stage makes
/// it InProgress, and finishing its last stage makes it InReview, from which staff mark it Completed. Any open
/// procedure can be Blocked (and later unblocked to where it was) or Cancelled, each with a reason. Completed and
/// Cancelled procedures can be Archived.
/// </para>
/// <para>
/// Stages are worked strictly in order. The current stage is the first one not finished; its notes can be edited until
/// it's finished, which locks it. An admin can unlock a finished stage to reopen just that stage, which moves an
/// InReview or Completed procedure back to InProgress.
/// </para>
/// </summary>
public static partial class ProcedureWorkflow
{
    public const string StageNotStarted = "NotStarted";
    public const string StageInProgress = "InProgress";
    public const string StageCompleted = "Completed";

    /// <summary>Work still to do: stages can be worked on.</summary>
    public static readonly IReadOnlyList<PatientProcedureStatus> OpenStatuses = [Unassigned, Assigned, InProgress, InReview];

    /// <summary>Finished with, one way or another: lists hide these unless asked.</summary>
    public static readonly IReadOnlyList<PatientProcedureStatus> ClosedStatuses = [Completed, Cancelled, Archived];

    public static bool IsOpen(PatientProcedureStatus status) => OpenStatuses.Contains(status);

    public static bool IsClosed(PatientProcedureStatus status) => ClosedStatuses.Contains(status);

    /// <summary>Blocking and cancelling need a reason, for the audit log.</summary>
    public static bool RequiresReason(PatientProcedureStatus status) => status is Blocked or Cancelled;

    /// <summary>The status changes staff can make by hand from the procedure's current status.</summary>
    public static IReadOnlyList<PatientProcedureStatus> AllowedStatusChanges(PatientProcedure procedure) => procedure.Status switch
    {
        Unassigned or Assigned or InProgress => [Blocked, Cancelled],
        InReview => [Completed, Blocked, Cancelled],
        Blocked => [procedure.BlockedFromStatus ?? StatusFromAssignments(procedure), Cancelled],
        Completed or Cancelled => [Archived],
        _ => []
    };

    /// <summary>Open and past its due date. Blocked procedures are waiting on something else, so never overdue.</summary>
    public static bool IsOverdue(PatientProcedure procedure, DateTime nowUtc) =>
        IsOpen(procedure.Status) && procedure.DueDateUtc < nowUtc;

    /// <summary>What an unstarted procedure's status should be: Assigned when someone's assigned, otherwise Unassigned.</summary>
    public static PatientProcedureStatus StatusFromAssignments(PatientProcedure procedure) =>
        procedure.Assignments.Count > 0 ? Assigned : Unassigned;

    public static bool CanEditDetails(PatientProcedure procedure) => procedure.Status is not (Cancelled or Archived);

    public static bool CanChangeAssignments(PatientProcedure procedure) => !IsClosed(procedure.Status);

    /// <summary>Only Cancelled and Archived procedures can be deleted, by an admin.</summary>
    public static bool CanBeDeleted(PatientProcedure procedure) => procedure.Status is Cancelled or Archived;

    public static bool CanWorkOnStages(PatientProcedure procedure) => IsOpen(procedure.Status);

    /// <summary>The stage being worked on: the first one not finished. Null once every stage is finished.</summary>
    public static PatientProcedureStage? CurrentStage(PatientProcedure procedure) =>
        procedure.Stages.OrderBy(s => s.StageOrder).FirstOrDefault(s => !s.IsCompleted);

    /// <summary>Every stage finished and locked (none reopened by an admin).</summary>
    public static bool AllStagesFinished(PatientProcedure procedure) =>
        procedure.Stages.Count > 0 && procedure.Stages.All(s => s.IsCompleted && s.IsLocked);

    /// <summary>Whether work has begun on any stage.</summary>
    public static bool HasStarted(PatientProcedure procedure) =>
        procedure.Stages.Any(s => s.StartedAtUtc is not null || s.IsCompleted);

    public static string StageState(PatientProcedureStage stage) =>
        stage.IsCompleted ? StageCompleted : stage.StartedAtUtc is null ? StageNotStarted : StageInProgress;

    /// <summary>Whether a stage's notes can be edited and it can be finished now.</summary>
    public static bool CanEditStage(PatientProcedure procedure, PatientProcedureStage stage) =>
        WhyStageCantBeEdited(procedure, stage) is null;

    /// <summary>Why a stage can't be edited now, or null if it can.</summary>
    public static string? WhyStageCantBeEdited(PatientProcedure procedure, PatientProcedureStage stage)
    {
        if (!CanWorkOnStages(procedure))
            return $"Stages can't be worked on while the procedure is {Describe(procedure.Status)}.";

        if (stage.IsCompleted)
            return stage.IsLocked ? $"The '{stage.Name}' stage is finished and locked. An admin can unlock it." : null;

        var current = CurrentStage(procedure);
        return current?.Id == stage.Id
            ? null
            : $"Stages are worked in order: finish the '{current?.Name}' stage first.";
    }

    /// <summary>Whether an admin can unlock a stage: it's finished and locked, and the procedure isn't blocked or closed off.</summary>
    public static bool CanUnlockStage(PatientProcedure procedure, PatientProcedureStage stage) =>
        stage.IsCompleted && stage.IsLocked && (IsOpen(procedure.Status) || procedure.Status == Completed);

    /// <summary>A status in words, e.g. "in review".</summary>
    public static string Describe(PatientProcedureStatus status) =>
        WordBoundary().Replace(status.ToString(), " $1").ToLowerInvariant();

    [GeneratedRegex("(?<=[a-z])([A-Z])")]
    private static partial Regex WordBoundary();
}
