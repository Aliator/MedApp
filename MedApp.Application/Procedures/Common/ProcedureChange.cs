using MedApp.Domain.Procedures.PatientProcedures;
using Action = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureAuditAction;
using static MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.Application.Procedures.Common;

/// <summary>
/// Applies one request's changes to a procedure. It keeps the status in step with the workflow (see
/// <see cref="ProcedureWorkflow"/>), stamps LastUpdated, and collects the audit entries to save with the change.
/// Handlers check that a change is allowed first; this only applies it.
/// </summary>
public sealed class ProcedureChange(PatientProcedure procedure, ProcedureActor actor, DateTime nowUtc)
{
    private readonly List<PatientProcedureAuditEntry> _auditEntries = [];

    public PatientProcedure Procedure => procedure;

    public DateTime NowUtc => nowUtc;

    public IReadOnlyList<PatientProcedureAuditEntry> AuditEntries => _auditEntries;

    /// <summary>Whether anything changed (every change is audited).</summary>
    public bool HasChanges => _auditEntries.Count > 0;

    public void Audit(PatientProcedureAuditAction action, object? details = null, PatientProcedureStage? stage = null)
    {
        _auditEntries.Add(ProcedureAudit.Entry(procedure, actor, action, nowUtc, stage, details));
        procedure.LastUpdated = nowUtc;
    }

    /// <summary>A status change chosen by staff (see <see cref="ProcedureWorkflow.AllowedStatusChanges"/>).</summary>
    public void ChangeStatus(PatientProcedureStatus to, string? reason) => SetStatus(to, reason, automatic: false);

    public void Assign(Guid userId, string username)
    {
        procedure.Assignments.Add(new PatientProcedureAssignment
        {
            PatientProcedureId = procedure.Id,
            UserId = userId,
            AssignedByUserId = actor.UserId,
            AssignedAtUtc = nowUtc
        });
        Audit(Action.Assigned, new { userId, username });
        FollowAssignments();
    }

    public void Unassign(PatientProcedureAssignment assignment, string username)
    {
        procedure.Assignments.Remove(assignment);
        Audit(Action.Unassigned, new { userId = assignment.UserId, username });
        FollowAssignments();
    }

    /// <summary>Records that work on a stage has begun (once); the procedure becomes InProgress if it wasn't.</summary>
    public void StartStage(PatientProcedureStage stage)
    {
        if (stage.StartedAtUtc is not null)
            return;

        stage.StartedAtUtc = nowUtc;
        stage.StartedByUserId = actor.UserId;
        stage.LastUpdated = nowUtc;
        Audit(Action.StageStarted, stage: stage);

        if (procedure.Status is Unassigned or Assigned)
            SetStatus(InProgress, null, automatic: true);
    }

    /// <summary>Replaces a stage's notes, starting the stage if needed. Nothing happens if they're unchanged.</summary>
    public void UpdateStageNotes(PatientProcedureStage stage, string notes)
    {
        if (stage.Notes == notes)
            return;

        StartStage(stage);
        var previous = stage.Notes;
        stage.Notes = notes;
        stage.LastUpdated = nowUtc;
        Audit(Action.StageNotesUpdated, new { from = previous, to = notes }, stage);
    }

    /// <summary>Finishes and locks a stage. Once every stage is finished, the procedure goes to InReview.</summary>
    public void FinishStage(PatientProcedureStage stage)
    {
        StartStage(stage);
        stage.IsCompleted = true;
        stage.IsLocked = true;
        stage.CompletedAtUtc = nowUtc;
        stage.CompletedByUserId = actor.UserId;
        stage.LastUpdated = nowUtc;
        Audit(Action.StageFinished, stage: stage);

        if (ProcedureWorkflow.AllStagesFinished(procedure))
            SetStatus(InReview, null, automatic: true);
    }

    /// <summary>Reopens a finished stage for editing; an InReview or Completed procedure goes back to InProgress.</summary>
    public void UnlockStage(PatientProcedureStage stage)
    {
        stage.IsLocked = false;
        stage.LastUpdated = nowUtc;
        Audit(Action.StageUnlocked, stage: stage);

        if (procedure.Status is InReview or Completed)
            SetStatus(InProgress, null, automatic: true);
    }

    private void SetStatus(PatientProcedureStatus to, string? reason, bool automatic)
    {
        var from = procedure.Status;
        if (from == to)
            return;

        procedure.BlockedFromStatus = to == Blocked ? from : null;
        procedure.Status = to;
        Audit(Action.StatusChanged, new { from = from.ToString(), to = to.ToString(), reason, automatic });
    }

    // Unassigned and Assigned only say whether someone is assigned. Once work starts, the status follows the stages.
    private void FollowAssignments()
    {
        var expected = ProcedureWorkflow.StatusFromAssignments(procedure);

        if (procedure.Status is Unassigned or Assigned)
            SetStatus(expected, null, automatic: true);
        else if (procedure is { Status: Blocked, BlockedFromStatus: Unassigned or Assigned })
            procedure.BlockedFromStatus = expected;
    }
}
