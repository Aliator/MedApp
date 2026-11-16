using MedApp.Application.Appointments.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Procedures.PatientProcedures;
using Action = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureAuditAction;
using static MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.Application.Procedures.Common;

/// <summary>
/// Applies one request's changes to a procedure. It keeps the status in step with the workflow (see
/// <see cref="ProcedureWorkflow"/>), stamps LastUpdated, and collects the audit entries to save with the change.
/// Handlers check that a change is allowed first; this only applies it.
/// <para>
/// It also keeps the procedure's stage visits in step (<see cref="RequestVisit"/>, <see cref="CancelRequestedVisit"/>):
/// the appointments it adds and their history entries are saved with the change too
/// (<c>IPatientProcedureRepository.SaveAsync(ProcedureChange, ...)</c>), in one save.
/// </para>
/// </summary>
public sealed class ProcedureChange(PatientProcedure procedure, ProcedureActor actor, DateTime nowUtc)
{
    public const string StageFinishedReason = "The stage was finished.";
    public const string ProcedureCancelledReason = "The procedure was cancelled.";

    private readonly List<PatientProcedureAuditEntry> _auditEntries = [];
    private readonly List<Appointment> _newAppointments = [];
    private readonly List<AppointmentEvent> _appointmentEvents = [];

    public PatientProcedure Procedure => procedure;

    /// <summary>Who made the change.</summary>
    public ProcedureActor Actor => actor;

    public DateTime NowUtc => nowUtc;

    public IReadOnlyList<PatientProcedureAuditEntry> AuditEntries => _auditEntries;

    /// <summary>Appointments the change asked for (Requested ones for stage visits), to add with it.</summary>
    public IReadOnlyList<Appointment> NewAppointments => _newAppointments;

    /// <summary>History entries of the appointments the change asked for or cancelled, to add with it.</summary>
    public IReadOnlyList<AppointmentEvent> AppointmentEvents => _appointmentEvents;

    /// <summary>Whether anything changed (every change is audited, or recorded in an appointment's history).</summary>
    public bool HasChanges => _auditEntries.Count > 0 || _appointmentEvents.Count > 0;

    /// <summary>
    /// Records an audit entry. Entries one change writes (a stage started, its notes saved, the status that followed,
    /// and the history of the appointments it tidied up) are each a tick (100 ns) after the one before, so they sort
    /// in the order they happened; their IDs are random.
    /// </summary>
    public void Audit(PatientProcedureAuditAction action, object? details = null, PatientProcedureStage? stage = null)
    {
        _auditEntries.Add(ProcedureAudit.Entry(procedure, actor, action, NextTime(), stage, details));
        procedure.LastUpdated = nowUtc;
    }

    /// <summary>
    /// Asks for a visit for a stage: a Requested appointment for the procedure's patient, procedure and stage, made by
    /// the app (on behalf of the actor), recorded in its history as automatic.
    /// </summary>
    public Appointment RequestVisit(PatientProcedureStage stage)
    {
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = procedure.PatientId,
            ProcedureId = procedure.Id,
            StageId = stage.Id,
            Status = AppointmentStatus.Requested,
            IsAutomatic = true,
            CreatedAtUtc = nowUtc,
            CreatedByUserId = actor.UserId,
            LastUpdated = nowUtc
        };

        var change = new AppointmentChange(appointment, actor, NextTime(), procedureTitle: procedure.Title, stageName: stage.Name);
        change.Request(automatic: true);
        appointment.LastUpdated = nowUtc;

        _newAppointments.Add(appointment);
        _appointmentEvents.AddRange(change.Events);
        return appointment;
    }

    /// <summary>Cancels one of the procedure's Requested appointments, automatically, saying why.</summary>
    public void CancelRequestedVisit(Appointment appointment, string reason)
    {
        if (appointment.Status != AppointmentStatus.Requested)
            return;

        var stageName = procedure.Stages.FirstOrDefault(s => s.Id == appointment.StageId)?.Name;
        var change = new AppointmentChange(appointment, actor, NextTime(), procedureTitle: procedure.Title, stageName: stageName);
        change.Cancel(reason, automatic: true);
        appointment.LastUpdated = nowUtc;

        _appointmentEvents.AddRange(change.Events);
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

    /// <summary>
    /// Finishes and locks a stage. Once every stage is finished, the procedure goes to InReview. Finishing a stage
    /// that involves a visit without an attended one (<paramref name="withoutVisit"/>) records that, the requirement
    /// and the reason given, in the StageFinished entry.
    /// </summary>
    public void FinishStage(PatientProcedureStage stage, FinishedWithoutVisit? withoutVisit = null)
    {
        StartStage(stage);
        stage.IsCompleted = true;
        stage.IsLocked = true;
        stage.CompletedAtUtc = nowUtc;
        stage.CompletedByUserId = actor.UserId;
        stage.LastUpdated = nowUtc;
        Audit(
            Action.StageFinished,
            withoutVisit is null
                ? null
                : new { withoutVisit = true, visitRequirement = withoutVisit.Requirement.ToString(), reason = withoutVisit.Reason },
            stage);

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

    /// <summary>The time for the next entry this change writes: each a tick after the one before.</summary>
    private DateTime NextTime() => nowUtc.AddTicks(_auditEntries.Count + _appointmentEvents.Count);

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

/// <summary>A stage finished without the attended visit it involves: its requirement, and why (required for Required).</summary>
public sealed record FinishedWithoutVisit(VisitRequirement Requirement, string? Reason);
