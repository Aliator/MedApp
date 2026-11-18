using MedApp.Application.Appointments.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Pathways.PatientPathways;
using Action = MedApp.Domain.Pathways.PatientPathways.PatientPathwayAuditAction;
using static MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.Application.Pathways.Common;

/// <summary>
/// Applies one request's changes to a pathway. It keeps the status in step with the workflow (see
/// <see cref="PathwayWorkflow"/>), stamps LastUpdated, and collects the audit entries to save with the change.
/// Handlers check that a change is allowed first; this only applies it.
/// <para>
/// It also keeps the pathway's stage visits in step (<see cref="RequestVisit"/>, <see cref="CancelRequestedVisit"/>):
/// the appointments it adds and their history entries are saved with the change too
/// (<c>IPatientPathwayRepository.SaveAsync(PathwayChange, ...)</c>), in one save.
/// </para>
/// </summary>
public sealed class PathwayChange(PatientPathway pathway, PathwayActor actor, DateTime nowUtc)
{
    public const string StageFinishedReason = "The stage was finished.";
    public const string PathwayCancelledReason = "The pathway was cancelled.";

    private readonly List<PatientPathwayAuditEntry> _auditEntries = [];
    private readonly List<Appointment> _newAppointments = [];
    private readonly List<AppointmentEvent> _appointmentEvents = [];
    private readonly List<PatientPathwayNote> _newNotes = [];
    private readonly List<PatientPathwayNoteVersion> _noteVersions = [];
    private readonly List<PatientPathwayNote> _deletedNotes = [];

    public PatientPathway Pathway => pathway;

    /// <summary>Who made the change.</summary>
    public PathwayActor Actor => actor;

    public DateTime NowUtc => nowUtc;

    public IReadOnlyList<PatientPathwayAuditEntry> AuditEntries => _auditEntries;

    /// <summary>Appointments the change asked for (Requested ones for stage visits), to add with it.</summary>
    public IReadOnlyList<Appointment> NewAppointments => _newAppointments;

    /// <summary>History entries of the appointments the change asked for or cancelled, to add with it.</summary>
    public IReadOnlyList<AppointmentEvent> AppointmentEvents => _appointmentEvents;

    /// <summary>Notes the change added, to add with it.</summary>
    public IReadOnlyList<PatientPathwayNote> NewNotes => _newNotes;

    /// <summary>The note versions the change wrote (one for each note added or edited), to add with it.</summary>
    public IReadOnlyList<PatientPathwayNoteVersion> NoteVersions => _noteVersions;

    /// <summary>Notes the change deleted (soft: marked deleted, and kept).</summary>
    public IReadOnlyList<PatientPathwayNote> DeletedNotes => _deletedNotes;

    /// <summary>Whether anything changed (every change is audited, or recorded in an appointment's history).</summary>
    public bool HasChanges => _auditEntries.Count > 0 || _appointmentEvents.Count > 0;

    /// <summary>
    /// Records an audit entry. Entries one change writes (a stage started, its notes saved, the status that followed,
    /// and the history of the appointments it tidied up) are each a tick (100 ns) after the one before, so they sort
    /// in the order they happened; their IDs are random.
    /// </summary>
    public void Audit(PatientPathwayAuditAction action, object? details = null, PatientPathwayStage? stage = null)
    {
        _auditEntries.Add(PathwayAudit.Entry(pathway, actor, action, NextTime(), stage, details));
        pathway.LastUpdated = nowUtc;
    }

    /// <summary>
    /// Asks for a visit for a stage: a Requested appointment for the pathway's patient, pathway and stage, made by
    /// the app (on behalf of the actor), recorded in its history as automatic.
    /// </summary>
    public Appointment RequestVisit(PatientPathwayStage stage)
    {
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = pathway.PatientId,
            PathwayId = pathway.Id,
            StageId = stage.Id,
            Status = AppointmentStatus.Requested,
            IsAutomatic = true,
            CreatedAtUtc = nowUtc,
            CreatedByUserId = actor.UserId,
            LastUpdated = nowUtc
        };

        var change = new AppointmentChange(appointment, actor, NextTime(), pathwayTitle: pathway.Title, stageName: stage.Name);
        change.Request(automatic: true);
        appointment.LastUpdated = nowUtc;

        _newAppointments.Add(appointment);
        _appointmentEvents.AddRange(change.Events);
        return appointment;
    }

    /// <summary>Cancels one of the pathway's Requested appointments, automatically, saying why.</summary>
    public void CancelRequestedVisit(Appointment appointment, string reason)
    {
        if (appointment.Status != AppointmentStatus.Requested)
            return;

        var stageName = pathway.Stages.FirstOrDefault(s => s.Id == appointment.StageId)?.Name;
        var change = new AppointmentChange(appointment, actor, NextTime(), pathwayTitle: pathway.Title, stageName: stageName);
        change.Cancel(reason, automatic: true);
        appointment.LastUpdated = nowUtc;

        _appointmentEvents.AddRange(change.Events);
    }

    /// <summary>A status change chosen by staff (see <see cref="PathwayWorkflow.AllowedStatusChanges"/>).</summary>
    public void ChangeStatus(PatientPathwayStatus to, string? reason) => SetStatus(to, reason, automatic: false);

    public void Assign(Guid userId, string username)
    {
        pathway.Assignments.Add(new PatientPathwayAssignment
        {
            PatientPathwayId = pathway.Id,
            UserId = userId,
            AssignedByUserId = actor.UserId,
            AssignedAtUtc = nowUtc
        });
        Audit(Action.Assigned, new { userId, username });
        FollowAssignments();
    }

    public void Unassign(PatientPathwayAssignment assignment, string username)
    {
        pathway.Assignments.Remove(assignment);
        Audit(Action.Unassigned, new { userId = assignment.UserId, username });
        FollowAssignments();
    }

    /// <summary>
    /// Adds a note to the pathway by the actor: version 1, written to the note versions too, and a NoteAdded audit entry
    /// with the note's ID and version, never its text. Handlers check first that notes can change
    /// (<see cref="PathwayWorkflow.WhyNotesCantChange"/>).
    /// </summary>
    public PatientPathwayNote AddNote(string text)
    {
        var note = new PatientPathwayNote
        {
            Id = Guid.NewGuid(),
            PatientPathwayId = pathway.Id,
            AuthorUserId = actor.UserId,
            Text = text,
            CreatedAtUtc = nowUtc,
            Version = 1
        };

        pathway.Notes.Add(note);
        _newNotes.Add(note);
        AddVersion(note);
        Audit(Action.NoteAdded, new { noteId = note.Id, version = note.Version });
        return note;
    }

    /// <summary>
    /// Replaces a note's text: its next version, written to the note versions too, and a NoteEdited audit entry with the
    /// note's ID and new version, never its text. Nothing happens if the text is unchanged. Handlers check first that
    /// the actor wrote it and that notes can change.
    /// </summary>
    public void EditNote(PatientPathwayNote note, string text)
    {
        if (note.Text == text)
            return;

        note.Text = text;
        note.Version++;
        note.LastEditedAtUtc = nowUtc;
        AddVersion(note);
        Audit(Action.NoteEdited, new { noteId = note.Id, version = note.Version });
    }

    /// <summary>
    /// Deletes a note, by an admin, with a reason (a fixed choice) and an optional detail: soft, so it's hidden from the
    /// pathway page but kept, with its versions. A NoteDeleted audit entry records its ID and latest version, the
    /// reason by name (<c>deletionReason</c>), the detail and the request it approves (if any), never its text. Any
    /// request waiting for it is no longer waiting. Handlers check first that the actor is an admin, the version they
    /// saw and that the patient's record can change.
    /// <para>
    /// The oldest note is the one #110's migration copied the old single notes field into, so deleting it also clears
    /// that field (<see cref="PatientPathway.LegacyNotes"/>), which would otherwise keep the deleted text, and could
    /// bring it back when the column is dropped.
    /// </para>
    /// </summary>
    public void DeleteNote(PatientPathwayNote note, NoteDeletionReason reason, string? detail, Guid? requestId = null)
    {
        var isOldest = pathway.Notes.OrderBy(n => n.CreatedAtUtc).ThenBy(n => n.Id).FirstOrDefault() == note;
        note.DeletedAtUtc = nowUtc;
        note.DeletedByUserId = actor.UserId;
        note.DeletionReason = reason;
        note.DeletionDetail = detail;
        note.PendingDeletionRequestId = null;
        _deletedNotes.Add(note);

        var clearsOldField = isOldest && !string.IsNullOrEmpty(pathway.LegacyNotes);
        if (clearsOldField)
            pathway.LegacyNotes = string.Empty;

        Audit(Action.NoteDeleted, new
        {
            noteId = note.Id,
            version = note.Version,
            deletionReason = reason.ToString(),
            detail,
            requestId,
            oldNotesFieldCleared = clearsOldField ? true : (bool?)null
        });
    }

    /// <summary>Records that work on a stage has begun (once); the pathway becomes InProgress if it wasn't.</summary>
    public void StartStage(PatientPathwayStage stage)
    {
        if (stage.StartedAtUtc is not null)
            return;

        stage.StartedAtUtc = nowUtc;
        stage.StartedByUserId = actor.UserId;
        stage.LastUpdated = nowUtc;
        Audit(Action.StageStarted, stage: stage);

        if (pathway.Status is Unassigned or Assigned)
            SetStatus(InProgress, null, automatic: true);
    }

    /// <summary>Replaces a stage's notes, starting the stage if needed. Nothing happens if they're unchanged.</summary>
    public void UpdateStageNotes(PatientPathwayStage stage, string notes)
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
    /// Finishes and locks a stage. Once every stage is finished, the pathway goes to InReview. Finishing a stage
    /// that involves a visit without an attended one (<paramref name="withoutVisit"/>) records that, the requirement
    /// and the reason given, in the StageFinished entry.
    /// </summary>
    public void FinishStage(PatientPathwayStage stage, FinishedWithoutVisit? withoutVisit = null)
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

        if (PathwayWorkflow.AllStagesFinished(pathway))
            SetStatus(InReview, null, automatic: true);
    }

    /// <summary>Reopens a finished stage for editing; an InReview or Completed pathway goes back to InProgress.</summary>
    public void UnlockStage(PatientPathwayStage stage)
    {
        stage.IsLocked = false;
        stage.LastUpdated = nowUtc;
        Audit(Action.StageUnlocked, stage: stage);

        if (pathway.Status is InReview or Completed)
            SetStatus(InProgress, null, automatic: true);
    }

    private void AddVersion(PatientPathwayNote note) => _noteVersions.Add(new PatientPathwayNoteVersion
    {
        Id = Guid.NewGuid(),
        NoteId = note.Id,
        Version = note.Version,
        PatientPathwayId = pathway.Id,
        PatientId = pathway.PatientId,
        Text = note.Text,
        UserId = actor.UserId,
        Username = actor.Username,
        AtUtc = nowUtc
    });

    /// <summary>The time for the next entry this change writes: each a tick after the one before.</summary>
    private DateTime NextTime() => nowUtc.AddTicks(_auditEntries.Count + _appointmentEvents.Count);

    private void SetStatus(PatientPathwayStatus to, string? reason, bool automatic)
    {
        var from = pathway.Status;
        if (from == to)
            return;

        pathway.BlockedFromStatus = to == Blocked ? from : null;
        pathway.Status = to;
        Audit(Action.StatusChanged, new { from = from.ToString(), to = to.ToString(), reason, automatic });
    }

    // Unassigned and Assigned only say whether someone is assigned. Once work starts, the status follows the stages.
    private void FollowAssignments()
    {
        var expected = PathwayWorkflow.StatusFromAssignments(pathway);

        if (pathway.Status is Unassigned or Assigned)
            SetStatus(expected, null, automatic: true);
        else if (pathway is { Status: Blocked, BlockedFromStatus: Unassigned or Assigned })
            pathway.BlockedFromStatus = expected;
    }
}

/// <summary>A stage finished without the attended visit it involves: its requirement, and why (required for Required).</summary>
public sealed record FinishedWithoutVisit(VisitRequirement Requirement, string? Reason);
