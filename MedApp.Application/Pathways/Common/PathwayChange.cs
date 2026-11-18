using MedApp.Application.Appointments.Common;
using MedApp.Application.Referrals.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Referrals;
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
/// <para>
/// A pathway accepted from a referral has its waiting-time clock (<see cref="WaitingClock"/>): the referral is loaded
/// with it (<see cref="PatientPathway.Referral"/>), and this stops the clock when the clock-stop stage is finished or
/// the pathway is discharged or cancelled, re-reads its target when the priority changes, and moves the pathway's
/// target date with it (<see cref="FollowClock"/>), writing the referral's history entries to save in the same save
/// (<see cref="ReferralEvents"/>).
/// </para>
/// </summary>
public sealed class PathwayChange(PatientPathway pathway, PathwayActor actor, DateTime nowUtc)
{
    public const string StageFinishedReason = "The stage was finished.";
    public const string PathwayCancelledReason = "The pathway was cancelled.";
    public const string StageSkippedReason = "The stage was skipped.";

    private readonly List<PatientPathwayAuditEntry> _auditEntries = [];
    private readonly List<Appointment> _newAppointments = [];
    private readonly List<AppointmentEvent> _appointmentEvents = [];
    private readonly List<PatientPathwayNote> _newNotes = [];
    private readonly List<PatientPathwayNoteVersion> _noteVersions = [];
    private readonly List<PatientPathwayNote> _deletedNotes = [];
    private readonly List<ReferralClockPause> _newPauses = [];
    private ReferralChange? _clock;

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

    /// <summary>History entries the change wrote for the referral the pathway was accepted from (its clock), to add with it.</summary>
    public IReadOnlyList<ReferralEvent> ReferralEvents => _clock?.Events ?? [];

    /// <summary>Clock pauses the change started, to add with it.</summary>
    public IReadOnlyList<ReferralClockPause> NewPauses => _newPauses;

    /// <summary>
    /// Whether anything changed (every change is audited, or recorded in an appointment's or the referral's history).
    /// </summary>
    public bool HasChanges => _auditEntries.Count > 0 || _appointmentEvents.Count > 0 || ReferralEvents.Count > 0;

    /// <summary>The referral's change, for its clock, if the pathway was accepted from one (loaded with it).</summary>
    private ReferralChange? Clock =>
        pathway.Referral is { } referral ? _clock ??= new ReferralChange(referral, actor, nowUtc) : null;

    /// <summary>
    /// Moves the pathway's target date to where its referral's clock says it should be (<see cref="WaitingClock.PathwayTargetOn"/>):
    /// the clock's target while it runs, the pause's review date while it's paused. A DetailsUpdated entry records it as a
    /// hand edit's is recorded (<c>changes.dueDate</c>, from and to), with <c>"source":"clock"</c>, so Insights and the
    /// timeline read it alike. Nothing for a pathway without a referral, a stopped clock, or no change.
    /// </summary>
    public void FollowClock()
    {
        if (pathway.Referral is not { } referral || WaitingClock.PathwayTargetOn(referral) is not { } target)
            return;

        var current = DueDates.ToUkDate(pathway.DueDateUtc);
        if (current == target)
            return;

        pathway.DueDateUtc = DueDates.EndOfDayUtc(target);
        Audit(Action.DetailsUpdated, new
        {
            changes = new Dictionary<string, object> { ["dueDate"] = new { from = current, to = target } },
            source = WaitingClock.AuditSource
        });
    }

    /// <summary>
    /// After the pathway's priority changed: its clock (running or paused) re-reads the target for the new priority
    /// from the specialty's table, and the target date follows a running clock.
    /// </summary>
    public void PriorityChanged()
    {
        if (Clock is not { } clock || pathway.Referral?.Specialty is not { } specialty)
            return;

        if (clock.ReTarget(specialty, pathway.Priority))
            FollowClock();
    }

    /// <summary>
    /// Pauses the referral's clock today, for a delay the patient chose or caused, until <paramref name="reviewOn"/>; the
    /// pathway's target date becomes the review date. The handler checks first that it may
    /// (<see cref="WaitingClock.WhyCantPause"/>).
    /// </summary>
    public ReferralClockPause PauseClock(ClockPauseReason reason, string? detail, DateOnly reviewOn)
    {
        var clock = Clock ?? throw new InvalidOperationException("The pathway has no referral, so no waiting-time clock.");
        var pause = clock.PauseClock(reason, detail, reviewOn);
        _newPauses.Add(pause);
        FollowClock();
        return pause;
    }

    /// <summary>
    /// Resumes the referral's paused clock today: the paused days move its target on, and the pathway's target date
    /// follows it. The handler checks first that it may (<see cref="WaitingClock.WhyCantResume"/>).
    /// </summary>
    public void ResumeClock()
    {
        var clock = Clock ?? throw new InvalidOperationException("The pathway has no referral, so no waiting-time clock.");
        var pause = WaitingClock.OpenPause(clock.Referral)
                    ?? throw new InvalidOperationException("The waiting-time clock isn't paused.");
        clock.ResumeClock(pause);
        FollowClock();
    }

    /// <summary>Stops the referral's clock, if the pathway has one still running or paused.</summary>
    private void StopClock(ClockStopReason reason) => Clock?.StopClock(reason);

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
    /// Finishes and locks a stage. Once every stage is finished (or skipped), the pathway goes to InReview. Finishing a
    /// stage that involves a visit without an attended one (<paramref name="withoutVisit"/>) records that, the
    /// requirement and the reason given, in the StageFinished entry.
    /// <para>
    /// Finishing a branch point for the first time with the clinician's <paramref name="branch"/> (checked first with
    /// <see cref="PathwayWorkflow.WhyBranchChoiceInvalid"/>) skips the other branches' stages after it, with reason
    /// <see cref="StageSkipReason.BranchNotChosen"/>, and records one BranchChosen entry listing them.
    /// </para>
    /// </summary>
    /// <returns>The stages the branch choice skipped (none without one).</returns>
    public IReadOnlyList<PatientPathwayStage> FinishStage(
        PatientPathwayStage stage, FinishedWithoutVisit? withoutVisit = null, string? branch = null)
    {
        var skipped = stage.ChoosesBranch && !stage.IsCompleted
            ? PathwayWorkflow.StagesSkippedByBranch(pathway, stage, branch)
            : [];
        var chosen = stage.ChoosesBranch && !stage.IsCompleted && !string.IsNullOrWhiteSpace(branch)
            ? PathwayWorkflow.Branches(pathway, stage).FirstOrDefault(b => PathwayWorkflow.SameBranch(b.Name, branch))?.Name
            : null;

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

        // The template's clock-stop stage: the wait ends here (once: a stopped clock never restarts).
        if (stage.StopsClock)
            StopClock(ClockStopReason.StopStageFinished);

        if (chosen is not null)
        {
            foreach (var other in skipped)
                MarkSkipped(other, StageSkipReason.BranchNotChosen, null);

            Audit(Action.BranchChosen, new
            {
                branch = chosen,
                skippedStageIds = skipped.Select(s => s.Id).ToList(),
                skippedStages = skipped.Select(s => s.Name).ToList()
            }, stage);
        }

        if (PathwayWorkflow.AllStagesFinished(pathway))
            SetStatus(InReview, null, automatic: true);

        return chosen is null ? [] : skipped;
    }

    /// <summary>
    /// Skips an optional stage by hand, with a reason (never <see cref="StageSkipReason.BranchNotChosen"/>) and its
    /// detail: a StageSkipped entry with the reason by name (<c>skipReason</c>) and the <c>detail</c>. Skipping is work
    /// on the pathway, so an unstarted one goes to InProgress; once every stage is finished or skipped, to InReview.
    /// Handlers check first that it can be skipped (<see cref="PathwayWorkflow.WhySkipNotAllowed"/>).
    /// </summary>
    public void SkipStage(PatientPathwayStage stage, StageSkipReason reason, string? detail)
    {
        MarkSkipped(stage, reason, detail);
        // A Required visit skipped says so, as a finish without the visit does.
        Audit(Action.StageSkipped, new
        {
            skipReason = reason.ToString(),
            detail = stage.SkipDetail,
            visitRequirement = PathwayWorkflow.SkipNeedsOverride(stage) ? stage.VisitRequirement.ToString() : null
        }, stage);

        if (pathway.Status is Unassigned or Assigned)
            SetStatus(InProgress, null, automatic: true);

        if (PathwayWorkflow.AllStagesFinished(pathway))
            SetStatus(InReview, null, automatic: true);
    }

    /// <summary>
    /// Reinstates a skipped stage, with a reason: it's to do again, in its place, so if it's before the current stage it
    /// becomes current, as unlocking reopens one. A StageReinstated entry records the reason and how it had been
    /// skipped (<c>skipReason</c>). An InReview or Completed pathway goes back to InProgress. Handlers check first that
    /// it can be reinstated (<see cref="PathwayWorkflow.CanReinstateStage"/>).
    /// </summary>
    public void ReinstateStage(PatientPathwayStage stage, string reason)
    {
        var previous = stage.SkipReason;
        stage.SkippedAtUtc = null;
        stage.SkippedByUserId = null;
        stage.SkipReason = null;
        stage.SkipDetail = null;
        stage.LastUpdated = nowUtc;
        Audit(Action.StageReinstated, new { reason, skipReason = previous?.ToString() }, stage);

        if (pathway.Status is InReview or Completed)
            SetStatus(InProgress, null, automatic: true);
    }

    private void MarkSkipped(PatientPathwayStage stage, StageSkipReason reason, string? detail)
    {
        stage.SkippedAtUtc = nowUtc;
        stage.SkippedByUserId = actor.UserId;
        stage.SkipReason = reason;
        stage.SkipDetail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();
        stage.LastUpdated = nowUtc;
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

        // Discharged or cancelled before the clock-stop stage (or with none): the wait ends. On hold doesn't pause it.
        if (to == Completed)
            StopClock(ClockStopReason.PathwayDischarged);
        else if (to == Cancelled)
            StopClock(ClockStopReason.PathwayCancelled);
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
