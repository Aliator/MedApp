using MedApp.Application.Appointments.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Notifications;
using MedApp.Domain.Procedures.PatientProcedures;
using AuditAction = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureAuditAction;
using Priority = MedApp.Domain.Procedures.PatientProcedures.PatientProcedurePriority;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// Replays one procedure's history, change by change, through the app's own rules, as its handlers apply them:
/// <see cref="ProcedureChange"/> for the statuses, stages, assignments and the audit; <see cref="StageVisits"/> for the
/// visits a stage asks for when it becomes current; <see cref="AppointmentChange"/> for booking, moving, cancelling
/// and recording appointments; <see cref="AppointmentNotifications"/>, the assignment notification and the monitor's
/// matching for what staff are told. Each visit is booked into a free time on the calendar of the person working on
/// the stage. Nothing is saved here: the results are kept until the replay is accepted.
/// </summary>
public sealed class ProcedureReplay
{
    private readonly DemoWorld _world;
    private readonly ProcedurePlan _plan;
    private readonly Random _random;
    private readonly DateTime _now;
    private readonly double _waitScale;
    private readonly PatientProcedure _procedure;
    private readonly List<PatientProcedureStage> _stages;

    private readonly List<PatientProcedureAuditEntry> _audit = [];
    private readonly List<Appointment> _appointments = [];
    private readonly List<AppointmentEvent> _events = [];
    private readonly List<Notification> _notifications = [];
    private readonly List<Touch> _touches = [];

    private StaffMember _lead;
    private readonly StaffMember? _secondAssignee;
    private readonly StaffMember _preAssessmentNurse;
    private readonly StaffMember _wardNurse;
    private readonly StaffMember _followUpNurse;

    /// <summary>Nothing in this procedure happens before this (after a correction, the next stage waits for it).</summary>
    private DateTime _floor;

    public ProcedureReplay(DemoWorld world, ProcedurePlan plan, StaffMember lead, double waitScale)
    {
        _world = world;
        _plan = plan;
        _random = world.Random;
        _now = world.Timeline.NowUtc;
        _waitScale = waitScale;
        _lead = lead;

        var nurses = new[] { world.Member("k.ellison"), world.Member("l.northcott") };
        _preAssessmentNurse = _random.NextDouble() < 0.8 ? nurses[0] : nurses[1];
        _wardNurse = _random.NextDouble() < 0.5 ? nurses[0] : world.Member(DemoStaff.LockedUsername);
        _followUpNurse = _random.NextDouble() < 0.6 ? nurses[1] : nurses[0];
        if (plan.SecondAssignee)
        {
            _secondAssignee = _random.NextDouble() < 0.6 ? nurses[0] : nurses[1];
            _preAssessmentNurse = _secondAssignee;
            _followUpNurse = _secondAssignee;
        }

        var id = Guid.NewGuid();
        var due = DueDates.ToUkDate(plan.CreatedUtc).AddDays(7 * (plan.Urgent ? plan.Spec.UrgentDueWeeks : plan.Spec.DueWeeks));
        // A copy of the template's stages, as CreatePatientProcedureHandler makes it.
        _procedure = new PatientProcedure
        {
            Id = id,
            PatientId = plan.Patient.Id,
            TemplateId = world.TemplateIds[plan.Pathway],
            TemplateName = plan.Spec.TemplateName,
            Title = plan.Kind.Title,
            Notes = NoteBuilder.ProcedureNote(plan.NoteContext(), _random),
            DueDateUtc = DueDates.EndOfDayUtc(due),
            Priority = plan.Priority,
            Status = Status.Unassigned,
            CreatedAt = plan.CreatedUtc,
            LastUpdated = plan.CreatedUtc,
            Stages = plan.Spec.Stages.Select((spec, index) =>
            {
                var definition = world.Definitions[spec.Definition];
                return new PatientProcedureStage
                {
                    Id = Guid.NewGuid(),
                    PatientProcedureId = id,
                    StageDefinitionId = definition.Id,
                    StageOrder = index + 1,
                    Name = definition.Name,
                    Description = definition.Description,
                    Instructions = definition.Instructions,
                    SnomedCode = definition.SnomedCode,
                    SnomedTerm = definition.SnomedTerm,
                    VisitRequirement = definition.VisitRequirement,
                    CreatedAt = plan.CreatedUtc,
                    LastUpdated = plan.CreatedUtc,
                };
            }).ToList(),
        };
        _stages = _procedure.Stages.OrderBy(s => s.StageOrder).ToList();
    }

    public PatientProcedure Procedure => _procedure;
    public StaffMember Lead => _lead;
    public IReadOnlyList<Appointment> Appointments => _appointments;
    public DateTime? DeleteAtUtc { get; private set; }
    public int Bypasses { get; private set; }

    /// <summary>Whether a staff member did anything in this procedure, or had an appointment, at or after a moment.</summary>
    public bool Involves(StaffMember member, DateTime fromUtc) =>
        _audit.Any(a => a.UserId == member.Id && a.AtUtc >= fromUtc)
        || _events.Any(e => e.UserId == member.Id && e.AtUtc >= fromUtc)
        || _appointments.Any(a => a.StaffUserId == member.Id && a.EndUtc >= fromUtc);

    /// <summary>The last moment anything happened to it.</summary>
    public DateTime LastActivityUtc =>
        _audit.Select(a => a.AtUtc).Concat(_events.Select(e => e.AtUtc)).Append(_procedure.CreatedAt).Max();

    /// <summary>Adds what the replay produced to the world: it's been accepted.</summary>
    public void Commit()
    {
        _world.Procedures.Add(_procedure);
        _world.Audit.AddRange(_audit);
        _world.Appointments.AddRange(_appointments);
        _world.AppointmentEvents.AddRange(_events);
        _world.Notifications.AddRange(_notifications);
        _world.Touches.AddRange(_touches);
        _world.Bypasses += Bypasses;
        if (DeleteAtUtc is { } at)
            _world.Deletions.Add((_procedure, at));
    }

    public void Run()
    {
        var created = _plan.CreatedUtc;
        _floor = created;
        var creator = Creator();

        Change(creator, created, change =>
        {
            change.Audit(AuditAction.Created, new
            {
                template = _plan.Spec.TemplateName,
                dueDate = DueDates.ToUkDate(_procedure.DueDateUtc),
                priority = _procedure.Priority.ToString(),
                stageCount = _procedure.Stages.Count,
            });
            // The first stage's visit, if it involves one.
            StageVisits.RequestForCurrentStage(change, []);
        });

        // An admin assigns it, usually within a day or so; the newest are sometimes still waiting.
        var assignAt = Working(created.AddDays(0.02 + _random.NextDouble() * 1.5));
        var leftWaiting = created > _now.AddDays(-4) && _random.NextDouble() < 0.5;
        if (leftWaiting || assignAt >= _now)
            return;

        var assigner = _world.AdminAt(assignAt);
        Assign(assigner, assignAt, _lead);
        if (_secondAssignee is { } second)
            Assign(assigner, assignAt.AddMinutes(_random.Next(2, 9)), second);

        var reached = assignAt;
        for (var index = 0; index < _stages.Count; index++)
        {
            var outcome = RunStage(index, ref reached);
            if (outcome == StageOutcome.Stopped)
                return;
        }

        // Every stage finished: it's in review until someone marks it completed.
        var completeAt = Working(reached.AddDays(_random.NextDouble() < 0.85 ? 0.1 + _random.NextDouble() * 3 : 6 + _random.NextDouble() * 14));
        if (completeAt >= _now)
            return;
        Change(_lead, completeAt, change => change.ChangeStatus(Status.Completed, null));

        // Older work is archived now and then, by an admin.
        var archiveAt = Working(completeAt.AddDays(14 + _random.NextDouble() * 60));
        if (archiveAt < _now.AddDays(-7) && _random.NextDouble() < 0.5)
            Change(_world.AdminAt(archiveAt), archiveAt, change => change.ChangeStatus(Status.Archived, null));
    }

    private enum StageOutcome
    {
        Finished,
        Stopped,
    }

    private StageOutcome RunStage(int index, ref DateTime reached)
    {
        var spec = _plan.Spec.Stages[index];
        var stage = _stages[index];

        // Blocking or cancelling: decided when the stage is reached, before work starts.
        if (index == _plan.FateStage && _plan.Fate is Fate.Blocked or Fate.Unblocked or Fate.Cancelled or Fate.Deleted)
        {
            var at = Working(Later(reached).AddDays(0.3 + _random.NextDouble() * 4));
            if (at >= _now)
                return StageOutcome.Stopped;

            if (_plan.Fate is Fate.Cancelled or Fate.Deleted)
            {
                Cancel(at);
                if (_plan.Fate == Fate.Deleted)
                {
                    var deleteAt = Working(at.AddDays(14 + _random.NextDouble() * 21));
                    if (deleteAt < _now)
                        DeleteAtUtc = deleteAt;
                }

                return StageOutcome.Stopped;
            }

            Change(_lead, at, change => change.ChangeStatus(Status.Blocked, NoteBuilder.BlockReason(_random)));
            if (_plan.Fate == Fate.Blocked)
                return StageOutcome.Stopped;

            var unblockAt = Working(at.AddDays(4 + _random.NextDouble() * 10));
            if (unblockAt >= _now)
                return StageOutcome.Stopped;
            Change(_lead, unblockAt, change =>
                change.ChangeStatus(_procedure.BlockedFromStatus ?? ProcedureWorkflow.StatusFromAssignments(_procedure), null));
            reached = unblockAt;
        }

        // A change of lead before this stage: the new one is assigned, the old one taken off.
        if (index == _plan.ReassignStage)
        {
            var at = Working(Later(reached).AddHours(1 + _random.NextDouble() * 20));
            var replacement = _world.Staff.FirstOrDefault(s => s.Kind is StaffKind.Surgeon && s.Id != _lead.Id && s.WorksAt(at));
            if (at < _now && replacement is not null && ProcedureWorkflow.CanChangeAssignments(_procedure))
            {
                var admin = _world.AdminAt(at);
                Assign(admin, at, replacement);
                var old = _procedure.Assignments.Single(a => a.UserId == _lead.Id);
                var oldLead = _lead;
                Change(admin, at.AddMinutes(1), change => change.Unassign(old, oldLead.Username));
                _lead = replacement;
            }
        }

        // Recovery follows the procedure on the same day, however late the list runs; everything else is done in working hours.
        // Two-week-wait referrals move faster; the waiting-list initiative cleared the diagnostic scheduling backlog lately.
        var pace = _waitScale * (_plan.Urgent && _plan.Pathway == Pathway.Diagnostic ? 0.4 : 1)
                   * (_plan.Pathway == Pathway.Diagnostic && spec.Definition == "Scheduling" && reached > _now.AddDays(-28) ? 0.3 : 1);
        var wait = Later(reached).AddDays(spec.Wait.Pick(_random) * pace);
        var startAt = spec.Definition == "Recovery" ? wait : Working(wait);
        if (startAt >= _now)
            return StageOutcome.Stopped;

        var bypass = index == _plan.BypassStage && stage.VisitRequirement == VisitRequirement.Required;
        var worker = bypass ? _world.Lead : WorkerFor(spec);
        Change(worker, startAt, change => change.StartStage(stage));

        if (_plan.EditNotes && index == 1)
            EditDetails(worker, startAt.AddMinutes(_random.Next(2, 30)), notes: NoteBuilder.ProcedureNoteUpdate(_procedure.Notes, _random));
        // A due date moved: at scheduling for surgery (the theatre date is known), otherwise part-way through.
        if (_plan.EditDueDate && (_plan.Pathway == Pathway.Surgical ? spec.Definition == "Scheduling" : index == 2))
        {
            var due = DueDates.ToUkDate(_procedure.DueDateUtc).AddDays(7 * _random.Next(3, 9));
            EditDetails(_world.AdminAt(startAt), Working(startAt.AddMinutes(_random.Next(5, 120))), due: due);
        }

        var occurrence = _plan.Spec.Stages.Take(index + 1).Count(s => s.Definition == spec.Definition);
        var occurrences = _plan.Spec.Stages.Count(s => s.Definition == spec.Definition);
        var context = _plan.NoteContext(occurrence, occurrences);

        var hasVisit = !bypass && stage.VisitRequirement != VisitRequirement.None && spec.VisitMinutes > 0
                       && (spec.VisitChance >= 1 || _random.NextDouble() < spec.VisitChance);

        DateTime notesAt;
        if (hasVisit)
        {
            var visit = Visit(index, stage, spec, worker, startAt);
            if (visit is not { } attendedAt)
            {
                MaybeProgressNote(stage, worker, startAt, context);
                return StageOutcome.Stopped;
            }

            notesAt = attendedAt.AddMinutes(_random.Next(2, 35));
        }
        else
        {
            var work = spec.Work.Pick(_random) * _waitScale;
            var finishAt = spec.Definition == "Recovery" ? startAt.AddDays(work) : Working(startAt.AddDays(work));
            notesAt = finishAt.AddMinutes(-_random.Next(1, 8));
            if (notesAt <= startAt)
                notesAt = startAt.AddMinutes(1);
        }

        var finishedAt = notesAt.AddMinutes(_random.Next(1, 10));
        if (finishedAt >= _now)
        {
            MaybeProgressNote(stage, worker, startAt, context);
            return StageOutcome.Stopped;
        }

        var note = NoteBuilder.StageNote(stage.Name, context, _random);
        Change(worker, notesAt, change => change.UpdateStageNotes(stage, note));
        Finish(stage, worker, finishedAt, bypass ? NoteBuilder.BypassReason(_random) : null);
        reached = finishedAt;

        // A diagnostic referral upgraded at triage: urgent, and due sooner.
        if (_plan.RaisePriority && spec.Definition == "Triage")
            EditDetails(worker, finishedAt.AddMinutes(_random.Next(1, 5)), priority: Priority.Urgent,
                due: DueDates.ToUkDate(_procedure.CreatedAt).AddDays(7 * _plan.Spec.UrgentDueWeeks));

        // An admin reopens a finished stage, it's corrected, and finished again.
        if (index == _plan.UnlockStage)
        {
            var unlockAt = Working(finishedAt.AddDays(0.1 + _random.NextDouble() * 2));
            if (unlockAt < _now.AddHours(-1) && ProcedureWorkflow.CanUnlockStage(_procedure, stage))
            {
                Change(_world.Lead, unlockAt, change => change.UnlockStage(stage));
                var correctedAt = unlockAt.AddMinutes(_random.Next(5, 40));
                Change(worker, correctedAt, change => change.UpdateStageNotes(stage, NoteBuilder.Correction(stage.Notes, context, _random)));
                var refinishedAt = correctedAt.AddMinutes(_random.Next(1, 5));
                Finish(stage, worker, refinishedAt, null);
                _floor = refinishedAt.AddMinutes(5);
            }
        }

        return StageOutcome.Finished;
    }

    /// <summary>
    /// A stage's visit: its Requested appointment is booked (by the booking coordinator, or the clinician) with the
    /// person working on the stage, in a free time on their calendar; sometimes moved, cancelled and rebooked, or
    /// missed and rebooked; then attended. Returns when the attendance was recorded, or null if the story stops first
    /// (not booked yet, booked ahead, or the procedure was cancelled).
    /// </summary>
    private DateTime? Visit(int index, PatientProcedureStage stage, StageSpec spec, StaffMember worker, DateTime startedAt)
    {
        var appointment = _appointments.FirstOrDefault(a => a.StageId == stage.Id && a.Status == AppointmentStatus.Requested);
        var booker = _random.NextDouble() < 0.8 ? _world.Coordinator : worker;
        var bookedAt = Working(startedAt.AddDays(0.02 + _random.NextDouble() * 2.5 * _waitScale));
        if (bookedAt >= _now || index == _plan.NeverBookStage)
            return null;

        var minutes = spec.VisitMinutes == spec.VisitMinutesMax
            ? spec.VisitMinutes
            : spec.VisitMinutes + 15 * _random.Next((spec.VisitMinutesMax - spec.VisitMinutes) / 15 + 1);
        var slot = FindSlot(worker, bookedAt.AddDays(spec.Lead.Pick(_random) * (_plan.Urgent ? 0.5 : 1)), minutes, spec.Prefer);
        if (slot is null)
            return null;

        if (appointment is null)
        {
            // Its request was cancelled (or never made): booked afresh, as from the calendar.
            appointment = NewBooked(stage, booker, bookedAt, worker, slot);
        }
        else
        {
            Book(appointment, stage, booker, bookedAt, worker, slot);
        }

        if (_random.NextDouble() < 0.22)
            UpdateNotes(appointment, stage, booker, bookedAt.AddMinutes(1), NoteBuilder.AppointmentNote(_random));

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var start = appointment.StartUtc!.Value;

            // The procedure is cancelled after the visit was booked: the booking is cancelled by hand.
            if (index == _plan.FateStage && _plan.Fate == Fate.CancelledAfterBooking)
            {
                var at = Working(bookedAt.AddMinutes((start - bookedAt).TotalMinutes * (0.3 + _random.NextDouble() * 0.4)));
                if (at < _now && at < start.AddHours(-2))
                {
                    Cancel(at);
                    return null;
                }
            }

            // Moved to another time, at the patient's request, before the day.
            var roll = _random.NextDouble();
            if (roll < 0.07)
            {
                var at = Working(bookedAt.AddMinutes((start - bookedAt).TotalMinutes * (0.2 + _random.NextDouble() * 0.5)));
                if (at < _now && at < start.AddHours(-12)
                    && FindSlot(worker, Max(start.AddDays(2), at.AddDays(3)), minutes, spec.Prefer) is { } moved)
                {
                    Release(appointment);
                    Book(appointment, stage, _world.Coordinator, at, worker, moved);
                    continue;
                }
            }
            // Cancelled (by the patient, or the clinician couldn't make it), and a new one booked.
            else if (roll < 0.12)
            {
                var at = Working(bookedAt.AddMinutes((start - bookedAt).TotalMinutes * (0.2 + _random.NextDouble() * 0.6)));
                if (at < _now && at < start.AddHours(-3))
                {
                    var reason = _random.NextDouble() < 0.7 ? NoteBuilder.PatientCancelReason(_random) : NoteBuilder.ClinicianCancelReason(_random);
                    CancelAppointment(appointment, stage, _world.Coordinator, at, reason);
                    var rebookAt = Working(at.AddMinutes(_random.Next(5, 600)));
                    if (rebookAt >= _now
                        || FindSlot(worker, Max(start.AddDays(1), rebookAt.AddDays(2)), minutes, spec.Prefer) is not { } next)
                        return null;
                    appointment = NewBooked(stage, _world.Coordinator, rebookAt, worker, next);
                    continue;
                }
            }

            if (start >= _now)
                return null;

            // Didn't attend: recorded, and another booked.
            var end = appointment.EndUtc!.Value;
            if (_random.NextDouble() < 0.05 && end.AddMinutes(20) < _now)
            {
                RecordOutcome(appointment, stage, worker, end.AddMinutes(_random.Next(5, 20)), AppointmentStatus.DidNotAttend);
                var rebookAt = Working(end.AddDays(0.5 + _random.NextDouble()));
                if (rebookAt >= _now || FindSlot(worker, rebookAt.AddDays(3), minutes, spec.Prefer) is not { } next)
                    return null;
                appointment = NewBooked(stage, _world.Coordinator, rebookAt, worker, next);
                continue;
            }

            break;
        }

        if (appointment.StartUtc >= _now)
            return null;

        var outcomeAt = appointment.EndUtc!.Value.AddMinutes(_random.Next(3, 50));
        if (outcomeAt >= _now)
            return null;

        if (!string.IsNullOrEmpty(appointment.Notes) && _random.NextDouble() < 0.3)
            UpdateNotes(appointment, stage, worker, appointment.StartUtc!.Value.AddMinutes(-_random.Next(30, 600)),
                NoteBuilder.AppointmentNoteUpdate(appointment.Notes, _random));

        RecordOutcome(appointment, stage, worker, outcomeAt, AppointmentStatus.Attended);
        return outcomeAt;
    }

    /// <summary>
    /// Finishes a stage as FinishProcedureStageHandler does: a Required visit must have been attended (or an admin
    /// finishes without it, with a reason); the stage's Requested appointments are cancelled; the next stage's visit is
    /// asked for.
    /// </summary>
    private void Finish(PatientProcedureStage stage, StaffMember worker, DateTime at, string? reason)
    {
        Change(worker, at, change =>
        {
            var next = _stages.FirstOrDefault(s => s.StageOrder > stage.StageOrder && !s.IsCompleted);
            var appointments = _appointments.Where(a => a.StageId == stage.Id || (next is not null && a.StageId == next.Id)).ToList();
            var stageAppointments = appointments.Where(a => a.StageId == stage.Id).ToList();

            if (ProcedureWorkflow.WhyStageNeedsVisit(stage, stageAppointments) is { } needsVisit && reason is null)
                throw new InvalidOperationException($"Seeding went wrong: {needsVisit}");

            var withoutVisit = ProcedureWorkflow.FinishesWithoutVisit(stage, stageAppointments)
                ? new FinishedWithoutVisit(stage.VisitRequirement, reason)
                : null;
            if (withoutVisit is { Requirement: VisitRequirement.Required })
                Bypasses++;

            change.FinishStage(stage, withoutVisit);
            foreach (var appointment in stageAppointments)
                change.CancelRequestedVisit(appointment, ProcedureChange.StageFinishedReason);
            StageVisits.RequestForCurrentStage(change, appointments);
        });
    }

    /// <summary>Cancels the procedure, as ChangePatientProcedureStatusHandler does, then the booked visits by hand.</summary>
    private void Cancel(DateTime at)
    {
        Change(_lead, at, change =>
        {
            change.ChangeStatus(Status.Cancelled, NoteBuilder.CancelReason(_random));
            foreach (var requested in _appointments.Where(a => a.Status == AppointmentStatus.Requested).ToList())
                change.CancelRequestedVisit(requested, ProcedureChange.ProcedureCancelledReason);
        });

        // The app never cancels a booked appointment itself: the coordinator does, once the patient's been told.
        foreach (var booked in _appointments.Where(a => a.Status == AppointmentStatus.Booked && a.StartUtc > at).ToList())
        {
            var stage = _stages.FirstOrDefault(s => s.Id == booked.StageId);
            CancelAppointment(booked, stage, _world.Coordinator, at.AddMinutes(_random.Next(10, 90)), NoteBuilder.ProcedureCancelledAppointmentReason);
        }
    }

    /// <summary>Applies one request's change at a moment, as a handler would: audit, visits, the monitor's notifications.</summary>
    private void Change(StaffMember who, DateTime at, Action<ProcedureChange> apply)
    {
        var change = new ProcedureChange(_procedure, who.Actor, at);
        apply(change);
        _audit.AddRange(change.AuditEntries);
        _appointments.AddRange(change.NewAppointments);
        _events.AddRange(change.AppointmentEvents);
        Touch(who, at);

        if (at >= _world.Timeline.NotificationsFromUtc)
            _notifications.AddRange(_world.Monitor(_procedure, change.AuditEntries, who.Id, at));
    }

    /// <summary>Assigns someone, as AssignPatientProcedureHandler does: they're told, unless they assigned themselves.</summary>
    private void Assign(StaffMember admin, DateTime at, StaffMember assignee)
    {
        if (_procedure.Assignments.Any(a => a.UserId == assignee.Id))
            return;

        var change = new ProcedureChange(_procedure, admin.Actor, at);
        change.Assign(assignee.Id, assignee.Username);
        _audit.AddRange(change.AuditEntries);
        Touch(admin, at);

        if (at < _world.Timeline.NotificationsFromUtc)
            return;

        if (assignee.Id != admin.Id && _world.WantsAssignmentNotifications(assignee, at))
            _notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = assignee.Id,
                Kind = NotificationKind.ProcedureAssigned,
                Title = _procedure.Title,
                Message = $"{admin.Username} assigned you to this procedure.",
                ProcedureId = _procedure.Id,
                CreatedAtUtc = at,
            });

        _notifications.AddRange(_world.Monitor(_procedure, change.AuditEntries, admin.Id, at));
    }

    /// <summary>A details edit, as UpdatePatientProcedureHandler records it: what changed, from and to.</summary>
    private void EditDetails(StaffMember who, DateTime at, string? notes = null, DateOnly? due = null, Priority? priority = null)
    {
        if (at >= _now || !ProcedureWorkflow.CanEditDetails(_procedure))
            return;

        var changes = new Dictionary<string, object>();
        if (notes is { } n && n != _procedure.Notes)
        {
            changes["notes"] = new { from = _procedure.Notes, to = n };
            _procedure.Notes = n;
        }

        if (due is { } d && d != DueDates.ToUkDate(_procedure.DueDateUtc))
        {
            changes["dueDate"] = new { from = DueDates.ToUkDate(_procedure.DueDateUtc), to = d };
            _procedure.DueDateUtc = DueDates.EndOfDayUtc(d);
        }

        if (priority is { } p && p != _procedure.Priority)
        {
            changes["priority"] = new { from = _procedure.Priority.ToString(), to = p.ToString() };
            _procedure.Priority = p;
        }

        if (changes.Count > 0)
            Change(who, at, change => change.Audit(AuditAction.DetailsUpdated, new { changes }));
    }

    private void MaybeProgressNote(PatientProcedureStage stage, StaffMember worker, DateTime startedAt, NoteContext context)
    {
        var at = startedAt.AddMinutes(_random.Next(5, 90));
        if (at < _now && _random.NextDouble() < 0.5 && ProcedureWorkflow.CanEditStage(_procedure, stage))
            Change(worker, at, change => change.UpdateStageNotes(stage, NoteBuilder.ProgressNote(stage.Name, context, _random)));
    }

    // Appointments -------------------------------------------------------------------------------------------

    private ScheduleAllocator.Slot? FindSlot(StaffMember staff, DateTime earliest, int minutes, DayKind prefer)
    {
        var slot = _world.Allocator.Find(staff.Id, Max(earliest, _floor), minutes, _random, _world.Calendars.Prefers(prefer), skipChance: 0.2);
        if (slot is not null)
            _world.Allocator.Reserve(staff.Id, slot.StartUtc, slot.EndUtc);
        return slot;
    }

    private void Release(Appointment appointment) =>
        _world.Allocator.Release(appointment.StaffUserId!.Value, appointment.StartUtc!.Value, appointment.EndUtc!.Value);

    /// <summary>Books a Requested appointment, or moves a Booked one, as BookAppointmentHandler does.</summary>
    private void Book(Appointment appointment, PatientProcedureStage? stage, StaffMember by, DateTime at, StaffMember staff, ScheduleAllocator.Slot slot)
    {
        var previousStatus = appointment.Status;
        var previousStaff = appointment.StaffUserId;
        var previousStart = appointment.StartUtc;
        var change = new AppointmentChange(appointment, by.Actor, at,
            previousStaff is { } id ? _world.UsernameOf(id) : null, _procedure.Title, stage?.Name);
        change.Book(staff.Id, staff.Username, slot.StartUtc, slot.EndUtc);
        _events.AddRange(change.Events);
        Touch(by, at);

        if (at >= _world.Timeline.NotificationsFromUtc)
            _notifications.AddRange(AppointmentNotifications.AfterBooking(appointment, previousStatus, previousStaff, previousStart, by.Actor, at));
    }

    /// <summary>A new appointment booked straight away, as CreateAppointmentHandler does when given a time.</summary>
    private Appointment NewBooked(PatientProcedureStage stage, StaffMember by, DateTime at, StaffMember staff, ScheduleAllocator.Slot slot)
    {
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = _procedure.PatientId,
            ProcedureId = _procedure.Id,
            StageId = stage.Id,
            Status = AppointmentStatus.Requested,
            IsAutomatic = false,
            CreatedAtUtc = at,
            CreatedByUserId = by.Id,
            LastUpdated = at,
        };
        var change = new AppointmentChange(appointment, by.Actor, at, procedureTitle: _procedure.Title, stageName: stage.Name);
        change.Book(staff.Id, staff.Username, slot.StartUtc, slot.EndUtc);
        _appointments.Add(appointment);
        _events.AddRange(change.Events);
        Touch(by, at);

        if (at >= _world.Timeline.NotificationsFromUtc)
            _notifications.AddRange(AppointmentNotifications.AfterBooking(appointment, AppointmentStatus.Requested, null, null, by.Actor, at));
        return appointment;
    }

    private void CancelAppointment(Appointment appointment, PatientProcedureStage? stage, StaffMember by, DateTime at, string reason)
    {
        var previous = appointment.Status;
        var change = new AppointmentChange(appointment, by.Actor, at, StaffName(appointment), _procedure.Title, stage?.Name);
        change.Cancel(reason);
        _events.AddRange(change.Events);
        if (previous == AppointmentStatus.Booked)
            Release(appointment);
        Touch(by, at);

        if (at >= _world.Timeline.NotificationsFromUtc)
            _notifications.AddRange(AppointmentNotifications.AfterCancelling(appointment, previous, by.Actor, at));
    }

    private void RecordOutcome(Appointment appointment, PatientProcedureStage stage, StaffMember by, DateTime at, AppointmentStatus outcome)
    {
        var change = new AppointmentChange(appointment, by.Actor, at, StaffName(appointment), _procedure.Title, stage.Name);
        change.RecordOutcome(outcome);
        _events.AddRange(change.Events);
        Touch(by, at);
    }

    private void UpdateNotes(Appointment appointment, PatientProcedureStage stage, StaffMember by, DateTime at, string notes)
    {
        if (at >= _now)
            return;
        var change = new AppointmentChange(appointment, by.Actor, at, StaffName(appointment), _procedure.Title, stage.Name);
        change.UpdateNotes(notes);
        _events.AddRange(change.Events);
    }

    private string? StaffName(Appointment appointment) =>
        appointment.StaffUserId is { } id ? _world.UsernameOf(id) : null;

    // People and times -------------------------------------------------------------------------------------

    private StaffMember Creator() => _plan.Pathway switch
    {
        Pathway.Surgical => _lead,
        Pathway.Physio => _lead,
        Pathway.Minor => _random.NextDouble() < 0.5 ? _lead : _world.Coordinator,
        _ => _world.Coordinator,
    };

    private StaffMember WorkerFor(StageSpec spec) => spec.Role switch
    {
        StageRole.Coordinator => _world.Coordinator,
        StageRole.PreAssessment => _preAssessmentNurse,
        StageRole.Ward => _wardNurse,
        StageRole.FollowUp => _followUpNurse,
        _ => _lead,
    };

    private void Touch(StaffMember who, DateTime at) =>
        _touches.Add(new Touch(who.Id, at, _procedure.PatientId, _procedure.Id));

    private DateTime Working(DateTime utc) => DemoTimeline.InWorkingHours(Max(utc, _floor), _random);

    private DateTime Later(DateTime utc) => Max(utc, _floor);

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
}
