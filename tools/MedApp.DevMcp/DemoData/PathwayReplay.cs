using MedApp.Domain.Teams;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Specimens.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Referrals.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Referrals;
using Priority = MedApp.Domain.Pathways.PatientPathways.PatientPathwayPriority;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// Replays one pathway's history, change by change, through the app's own rules, as its handlers apply them:
/// <see cref="PathwayChange"/> for the statuses, stages, assignments and the audit; <see cref="StageVisits"/> for the
/// visits a stage asks for when it becomes current; <see cref="AppointmentChange"/> for booking, moving, cancelling
/// and recording appointments; <see cref="PathwayCreation"/>, <see cref="StageFinishing"/> and
/// <see cref="PathwayDetails"/> for what the create, finish and edit handlers do around them;
/// <see cref="AppointmentNotifications"/>, <see cref="AssignmentNotifications"/> and the monitor's own matching for what
/// staff are told. Each visit is booked into a free time on the calendar of the person working on
/// the stage. Nothing is saved here: the results are kept until the replay is accepted.
/// <para>
/// A pathway from a referral starts with it: registered by the booking coordinator (<see cref="DemoReferrals"/>), now
/// and then moved here from another team, then accepted by a clinician of its team, who creates the pathway
/// as AcceptReferralHandler does, with its target date from the referral's waiting-time clock. From then on the
/// pathway's changes carry the clock (<see cref="PathwayChange"/> stops it when the clock-stop stage is finished, or the
/// pathway is discharged or cancelled, and moves the target date with a pause or a new priority); its lead pauses it
/// when the patient asks to wait, and sets the target date by hand once it has stopped.
/// </para>
/// </summary>
public sealed partial class PathwayReplay
{
    private readonly DemoWorld _world;
    private readonly PathwayPlan _plan;
    private readonly Random _random;
    private readonly DateTime _now;
    private readonly double _waitScale;
    private readonly PatientPathway _pathway;
    private readonly string _templateName;
    private readonly DateOnly _dueDate;
    /// <summary>The pathway's stage at a template place (by the spec's index), or null where it hasn't grown (MC10b).</summary>
    private PatientPathwayStage? StageAt(int index) => _pathway.Stages.FirstOrDefault(s => s.TemplateStageOrder == index + 1);

    private readonly List<PatientPathwayAuditEntry> _audit = [];
    private readonly List<PatientPathwayNoteVersion> _noteVersions = [];
    private readonly List<Appointment> _appointments = [];
    private readonly List<AppointmentEvent> _events = [];
    private readonly List<Notification> _notifications = [];
    private readonly List<Touch> _touches = [];
    private readonly List<ReferralEvent> _referralEvents = [];
    private readonly List<PathwayAccess> _grants = [];
    private bool _passedToLab;

    /// <summary>The referral it was accepted from, with its clock; null for a pathway created directly.</summary>
    private Referral? _referral;

    private StaffMember _lead;
    private readonly StaffMember? _secondAssignee;
    private readonly StaffMember _samplesTaker;
    private readonly StaffMember _followUpNurse;

    /// <summary>Nothing in this pathway happens before this (after a correction, the next stage waits for it).</summary>
    private DateTime _floor;

    public PathwayReplay(DemoWorld world, PathwayPlan plan, StaffMember lead, double waitScale)
    {
        _world = world;
        _plan = plan;
        _random = world.Random;
        _now = world.Timeline.NowUtc;
        _waitScale = waitScale;
        _lead = lead;

        // Bloods and samples are taken by the healthcare assistant, or the skin cancer nurse specialist when she's free;
        // follow-up is the nurse specialist's for that service (for diabetes, the lead's own when the nurse leads it).
        var skinNurse = world.Member(DemoStaff.SkinCancerNurseUsername);
        var diabetesNurse = world.Member(DemoStaff.DiabetesNurseUsername);
        _samplesTaker = _random.NextDouble() < 0.65 ? world.Member(DemoStaff.LockedUsername) : skinNurse;
        _followUpNurse = plan.PathwayKind == PathwayKind.Diabetes ? diabetesNurse : skinNurse;
        if (plan.SecondAssignee)
        {
            // Assigned alongside the lead: the nurse specialist for the service; the pharmacist when the nurse leads it.
            _secondAssignee = plan.PathwayKind != PathwayKind.Diabetes ? skinNurse
                : lead.Id == diabetesNurse.Id ? world.Member(DemoStaff.PharmacistUsername)
                : diabetesNurse;
        }

        var template = world.Templates[plan.PathwayKind];
        _templateName = template.Name;
        // From a referral, the clock's target: the wait counts from the day it was received (no pauses before triage),
        // to the team's target for the priority the triager chose. Otherwise a target date set by hand.
        _dueDate = plan.FromReferral
            ? plan.ReceivedOn.AddDays(WaitingClock.TargetDaysFor(world.Teams[template.TeamId], plan.ChosenPriority))
            : plan.PlannedDueDate;
        // A copy of the template's stages, made as CreatePatientPathwayHandler (or AcceptReferralHandler) makes it.
        _pathway = PathwayCreation.FromTemplate(template, plan.Patient.Id, plan.Kind.Title, _dueDate, plan.ChosenPriority, plan.CreatedUtc);
        ApplyStageRulesOfTheTime();
    }

    public PatientPathway Pathway => _pathway;
    public StaffMember Lead => _lead;
    public IReadOnlyList<Appointment> Appointments => _appointments;
    public DateTime? DeleteAtUtc { get; private set; }
    public int Bypasses { get; private set; }

    /// <summary>Whether a staff member did anything in this pathway, or had an appointment, at or after a moment.</summary>
    public bool Involves(StaffMember member, DateTime fromUtc) =>
        _audit.Any(a => a.UserId == member.Id && a.AtUtc >= fromUtc)
        || _events.Any(e => e.UserId == member.Id && e.AtUtc >= fromUtc)
        || _appointments.Any(a => a.StaffUserId == member.Id && a.EndUtc >= fromUtc);

    /// <summary>The last moment anything happened to it.</summary>
    public DateTime LastActivityUtc =>
        _audit.Select(a => a.AtUtc).Concat(_events.Select(e => e.AtUtc)).Append(_pathway.CreatedAt).Max();

    /// <summary>Adds what the replay produced to the world: it's been accepted.</summary>
    public void Commit()
    {
        _plan.PathwayId = _pathway.Id;
        if (_plan.Side is { } site)
            _world.Sites[_pathway.Id] = site;
        _world.Pathways.Add(_pathway);
        _world.Audit.AddRange(_audit);
        _world.NoteVersions.AddRange(_noteVersions);
        _world.Appointments.AddRange(_appointments);
        _world.AppointmentEvents.AddRange(_events);
        _world.Notifications.AddRange(_notifications);
        _world.Touches.AddRange(_touches);
        if (_referral is not null)
            _world.Referrals.Add(_referral);
        _world.ReferralEvents.AddRange(_referralEvents);
        _world.PathwayAccess.AddRange(_grants);
        _world.Bypasses += Bypasses;
        if (DeleteAtUtc is { } at)
            _world.Deletions.Add((_pathway, at));
        CommitLab();
    }

    public void Run()
    {
        RunStory();
        // The lab reports what it has whatever happened to the pathway since (on hold, cancelled): a report is never stuck.
        FinishOpenSpecimens();
    }

    private void RunStory()
    {
        var created = _plan.CreatedUtc;
        _floor = created;
        var firstNote = NoteBuilder.PathwayNote(_plan.NoteContext(), _random);

        if (_plan.FromReferral)
        {
            Accept(created, firstNote);
        }
        else
        {
            // Created directly by its clinician (a recall, or a course asked for in clinic): its Created audit entry, its
            // first note (often none), and the first stage's visit if it involves one, as CreatePatientPathwayHandler
            // records them.
            Change(_lead, created, change => PathwayCreation.Record(change, _templateName, _dueDate, firstNote));
        }

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
        for (var index = 0; index < _plan.Spec.Stages.Length; index++)
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

    /// <summary>
    /// Its referral's story up to triage, then the acceptance that creates it: registered by the booking coordinator;
    /// now and then moved here first by a triager of the team it came under; accepted by a clinician of this
    /// team (the lead, usually), who creates the pathway (its Created entry names the referral) with the priority
    /// they chose and the clock's target date, as AcceptReferralHandler does, in one change. The coordinator is told.
    /// </summary>
    private void Accept(DateTime created, string? firstNote)
    {
        var world = _world;
        var team = world.Teams[world.Templates[_plan.PathwayKind].TeamId];
        var registeredUnder = _plan.RedirectedFrom is { } from ? world.Team(from) : team;
        var coordinator = world.Coordinator;

        var (referral, registered) = DemoReferrals.Register(_pathway.PatientId, registeredUnder, _plan.ReceivedOn, _plan.RequestedPriority,
            ReferralTexts.Reason(_plan.Kind, _random), coordinator, _plan.RegisteredUtc, _random);
        _referral = referral;
        _referralEvents.AddRange(registered);
        _touches.Add(new Touch(coordinator.Id, _plan.RegisteredUtc, _pathway.PatientId, null, referral.Id));

        if (_plan.RedirectedFrom is { } previous)
        {
            var mover = DemoReferrals.Triager(world, previous, _plan.RedirectedUtc)
                        ?? throw new InvalidOperationException($"Seeding went wrong: nobody could triage {previous} referrals.");
            var move = DemoReferrals.Triage(referral, mover, _plan.RedirectedUtc);
            move.RedirectToTeam(team, ReferralTexts.RedirectDetail(previous, team.Name));
            referral.Team = team;
            Triaged(move, mover, _plan.RedirectedUtc, previous);
        }

        var triager = DemoReferrals.Triager(world, team.Name, created, _lead)
                      ?? throw new InvalidOperationException($"Seeding went wrong: nobody could triage {team.Name} referrals.");
        var accept = DemoReferrals.Triage(referral, triager, created);
        Change(triager, created, change => PathwayCreation.Record(change, _templateName, _dueDate, firstNote, referral.Id));
        accept.Accept(_pathway.Id, _templateName, _plan.ChosenPriority);
        accept.ReTarget(team, _plan.ChosenPriority);
        if (WaitingClock.PathwayTargetOn(referral) != _dueDate)
            throw new InvalidOperationException("Seeding went wrong: the pathway's target date isn't its clock's.");
        Triaged(accept, triager, created);

        // From now on its changes carry the clock, as the pathway repository loads the referral with it.
        _pathway.Referral = referral;
    }

    /// <summary>A triage decision's history entries, the triager's look at the referral, and the coordinator's notification.</summary>
    private void Triaged(ReferralChange change, StaffMember triager, DateTime at, string? previousTeam = null)
    {
        _referralEvents.AddRange(change.Events);
        _touches.Add(new Touch(triager.Id, at, _pathway.PatientId, null, change.Referral.Id));
        if (DemoReferrals.Told(_world, change.Referral, triager, at, previousTeam) is { } told)
            _notifications.Add(told);
    }

    /// <summary>
    /// The patient asks to wait when a stage is reached: the lead pauses the clock (as PauseClockHandler does, through the
    /// pathway's change, so its target date becomes the review date). Then either it's resumed weeks later (the target
    /// moves on by the days paused) and the story goes on, or it's still paused now: with its review date ahead, or,
    /// for one, long past. Returns whether the story stops here.
    /// </summary>
    private bool PauseClock(ref DateTime reached)
    {
        if (_referral is not { } referral || WaitingClock.WhyCantPause(referral) is not null || !PathwayWorkflow.IsOpen(_pathway.Status))
            return false;

        var pauseAt = Working(Later(reached).AddDays(0.3 + _random.NextDouble() * 2));
        if (pauseAt >= _now)
            return false;

        var pausedOn = DueDates.ToUkDate(pauseAt);
        var today = _world.Timeline.Today;
        var reviewOn = _plan.Pause switch
        {
            PauseFate.ReviewOverdue => pausedOn.AddDays(_random.Next(21, 36)),
            PauseFate.StillPaused => pausedOn.AddDays(_random.Next(21, 61)) is var review && review > today ? review : today.AddDays(_random.Next(3, 22)),
            _ => pausedOn.AddDays(_random.Next(14, 61)),
        };
        reviewOn = reviewOn > WaitingClock.LatestReviewOn(pausedOn) ? WaitingClock.LatestReviewOn(pausedOn) : reviewOn;

        var reason = _plan.PauseReason;
        Change(_lead, pauseAt, change => change.PauseClock(reason, ReferralTexts.PauseDetail(reason, _random), reviewOn));
        if (_plan.Pause != PauseFate.Resumed)
            return true;

        var resumeAt = Working(pauseAt.AddDays(_random.Next(5, reviewOn.DayNumber - pausedOn.DayNumber + 1)));
        if (resumeAt >= _now)
            return true;
        Change(_lead, resumeAt, change => change.ResumeClock());
        reached = resumeAt;
        _floor = Max(_floor, resumeAt);
        return false;
    }

    private StageOutcome RunStage(int index, ref DateTime reached)
    {
        var spec = _plan.Spec.Stages[index];
        // A stage of an outcome the clinician didn't record never appears (MC10b): nothing to do.
        if (StageAt(index) is not { } stage || !PathwayWorkflow.IsToDo(stage))
            return StageOutcome.Finished;

        // The patient asked to wait: the clock is paused (and maybe resumed) before the stage is started.
        if (index == _plan.PauseStage && _plan.Pause != PauseFate.None && PauseClock(ref reached))
            return StageOutcome.Stopped;

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
                change.ChangeStatus(_pathway.BlockedFromStatus ?? PathwayWorkflow.StatusFromAssignments(_pathway), null));
            reached = unblockAt;
        }

        // A change of lead before this stage: the new one is assigned, the old one taken off.
        if (index == _plan.ReassignStage)
        {
            var at = Working(Later(reached).AddHours(1 + _random.NextDouble() * 20));
            var replacement = _world.Staff.FirstOrDefault(s => s.Kind is StaffKind.Dermatologist or StaffKind.SkinGp && s.Id != _lead.Id && s.WorksAt(at));
            if (at < _now && replacement is not null && PathwayWorkflow.CanChangeAssignments(_pathway))
            {
                var admin = _world.AdminAt(at);
                Assign(admin, at, replacement);
                var old = _pathway.Assignments.Single(a => a.UserId == _lead.Id);
                var oldLead = _lead;
                Change(admin, at.AddMinutes(1), change => change.Unassign(old, oldLead.Username));
                _lead = replacement;
            }
        }

        // Urgent referrals on the two suspected cancer routes move faster; the extra visiting clinics lately cut the wait
        // for a first post-menopausal bleeding visit.
        var pace = _waitScale * (_plan.Urgent && _plan.PathwayKind is PathwayKind.PostMenopausalBleeding or PathwayKind.SkinLesion ? 0.4 : 1)
                   * (_plan.PathwayKind == PathwayKind.PostMenopausalBleeding && spec.Definition == DemoPathwayKinds.FirstClinicVisit && reached > _now.AddDays(-28) ? 0.3 : 1);
        var startAt = Working(Later(reached).AddDays(spec.Wait.Pick(_random) * pace));
        if (startAt >= _now)
            return StageOutcome.Stopped;

        // An optional stage the patient declines is skipped (and, for one, reinstated when they change their mind).
        if (MaybeSkip(stage, ref reached, ref startAt) is { } skipped)
            return skipped;

        var bypass = index == _plan.BypassStage && stage.VisitRequirement == VisitRequirement.Required;
        var worker = bypass ? _world.Lead : WorkerFor(spec);
        Change(worker, startAt, change => change.StartStage(stage));

        // The patient withdraws consent before the biopsy: the pathway goes on hold.
        if (ConsentWithdrawn(stage, worker, startAt))
            return StageOutcome.Stopped;

        // A skin lesion's results go to the skin cancer team's MDT first: the visit and the stage's finish come after.
        if (stage.Name == DemoPathwayKinds.ResultsAndTeamReview && _plan.PathwayKind == PathwayKind.SkinLesion && LabAndLetters(startAt))
        {
            if (Mdt(stage, startAt) is not { } discussed)
            {
                MaybeProgressNote(stage, worker, startAt, _plan.NoteContext());
                return StageOutcome.Stopped;
            }

            _floor = Max(_floor, discussed);
        }

        // The pathway's first note brought up to date by whoever wrote it (only a note's author can edit it), or, when
        // there's none, a note from the person starting this stage.
        if (_plan.EditNotes && index == 1)
            UpdateFirstNote(worker, startAt.AddMinutes(_random.Next(2, 30)));
        // A due date moved: for a prostate assessment once the visiting clinic's date is known, otherwise part-way through.
        if (_plan.EditDueDate && (_plan.PathwayKind == PathwayKind.Prostate ? spec.Definition == DemoPathwayKinds.FirstClinicVisit : index == 2))
        {
            var due = DueDates.ToUkDate(_pathway.DueDateUtc).AddDays(7 * _random.Next(3, 9));
            EditDetails(_world.AdminAt(startAt), Working(startAt.AddMinutes(_random.Next(5, 120))), due: due);
        }

        // Counting only the stages that run (not those of a branch not chosen, nor skipped).
        var occurrence = Enumerable.Range(0, index + 1).Count(i => _plan.Spec.Stages[i].Definition == spec.Definition && StageAt(i) is { IsSkipped: false });
        var occurrences = Enumerable.Range(0, _plan.Spec.Stages.Length).Count(i => _plan.Spec.Stages[i].Definition == spec.Definition && StageAt(i) is { IsSkipped: false });
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

            // What the visit took to the lab, once the centre tracked specimens.
            if (TakesSpecimens(stage) && LabAndLetters(startAt) && _plan.Fate != Fate.Deleted)
                notesAt = Max(notesAt, TakeSpecimens(stage, worker, attendedAt).AddMinutes(_random.Next(2, 15)));
        }
        else
        {
            var work = spec.Work.Pick(_random) * _waitScale;
            var finishAt = Working(startAt.AddDays(work));
            notesAt = finishAt.AddMinutes(-_random.Next(1, 8));
            if (notesAt <= startAt)
                notesAt = startAt.AddMinutes(1);

            // The lab's stage waits for its reports and their sign-off; a discharge letter stage for its letter.
            DateTime? ready = finishAt;
            if (spec.Role == StageRole.Lab && _specimens.Any(s => SpecimenWorkflow.IsOpen(s.Status)))
                ready = RunLab(stage, worker, startAt, finishAt);
            else if (stage.NeedsLetterKind is not null)
                ready = PlanLetter(stage, worker, startAt);

            if (ready is not { } readyAt)
            {
                MaybeProgressNote(stage, worker, startAt, context);
                return StageOutcome.Stopped;
            }

            if (readyAt > finishAt)
                notesAt = readyAt.AddMinutes(_random.Next(2, 20));
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
        MaybePathwayNote(spec, stage, worker, finishedAt);

        // The clock stopped with this stage (decision 36): the target date is the lead's to set again, by hand, for the
        // rest of the pathway (the plan's, from when it was accepted), unless the clock's is later.
        if (stage.StopsClock && _referral is { } referral && WaitingClock.IsStopped(referral) && !PathwayWorkflow.AllStagesFinished(_pathway)
            && _plan.PlannedDueDate > DueDates.ToUkDate(_pathway.DueDateUtc))
            EditDetails(_lead, Working(finishedAt.AddMinutes(_random.Next(3, 90))), due: _plan.PlannedDueDate);

        // An admin reopens a finished stage, it's corrected, and finished again.
        if (index == _plan.UnlockStage)
        {
            var unlockAt = Working(finishedAt.AddDays(0.1 + _random.NextDouble() * 2));
            if (unlockAt < _now.AddHours(-1) && PathwayWorkflow.CanUnlockStage(_pathway, stage))
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
    /// (not booked yet, booked ahead, or the pathway was cancelled).
    /// </summary>
    private DateTime? Visit(int index, PatientPathwayStage stage, StageSpec spec, StaffMember worker, DateTime startedAt)
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

            // The pathway is cancelled after the visit was booked: the booking is cancelled by hand.
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

            // Didn't attend: recorded, what to do next decided by the booking coordinator, and another booked.
            var end = appointment.EndUtc!.Value;
            if (_random.NextDouble() < 0.05 && end.AddMinutes(20) < _now)
            {
                RecordOutcome(appointment, stage, worker, end.AddMinutes(_random.Next(5, 20)), AppointmentStatus.DidNotAttend);
                if (DnaFollowUp(appointment, stage, end) is not { } decidedAt)
                    return null;
                var rebookAt = Working(Max(end.AddDays(0.5 + _random.NextDouble()), decidedAt.AddMinutes(5)));
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
    /// Finishes a stage as FinishPathwayStageHandler does (<see cref="StageFinishing"/>): a Required visit must have
    /// been attended (or an admin finishes without it, with a reason); the stage's Requested appointments are
    /// cancelled; the next stage's visit is asked for.
    /// </summary>
    private void Finish(PatientPathwayStage stage, StaffMember worker, DateTime at, string? reason)
    {
        Change(worker, at, change =>
        {
            var touched = StageFinishing.StagesTouchedByFinish(_pathway, stage);
            var appointments = _appointments.Where(a => a.StageId is { } id && touched.Contains(id)).ToList();

            if (PathwayWorkflow.WhyStageNeedsVisit(stage, appointments.Where(a => a.StageId == stage.Id).ToList()) is { } needsVisit && reason is null)
                throw new InvalidOperationException($"Seeding went wrong: {needsVisit}");

            // The gates every finish path checks (StageGates), with this pathway's specimens, results, letter and consent.
            var open = _specimens.Where(s => SpecimenWorkflow.IsOpen(s.Status)).ToList();
            var awaiting = _results.Where(r => r.IsAwaitingSignOff).ToList();
            var last = StageGates.IsLastToFinish(_pathway, stage);
            var gate = StageGates.WhyLabCaseOpen(stage, MedApp.Domain.Teams.Team.LabId)
                       ?? (last ? StageGates.WhyCantFinish(_pathway, stage, open) : null)
                       ?? (stage.WaitsForResults || last ? StageGates.WhyResultsAwaiting(_pathway, stage, awaiting) : null)
                       ?? StageGates.WhyConsentMissing(stage, _consents)
                       ?? (stage.NeedsLetterKind is not null && _letter?.SentUtc is null ? "Its letter isn't sent." : null);
            if (gate is not null)
                throw new InvalidOperationException($"Seeding went wrong: {gate}");

            if (StageFinishing.Finish(change, stage, appointments, reason, OutcomeFor(stage)) is { Requirement: VisitRequirement.Required })
                Bypasses++;
        });
    }

    /// <summary>Cancels the pathway, as ChangePatientPathwayStatusHandler does, then the booked visits by hand.</summary>
    private void Cancel(DateTime at)
    {
        Change(_lead, at, change =>
        {
            change.ChangeStatus(Status.Cancelled, NoteBuilder.CancelReason(_random));
            foreach (var requested in _appointments.Where(a => a.Status == AppointmentStatus.Requested).ToList())
                change.CancelRequestedVisit(requested, PathwayChange.PathwayCancelledReason);
        });

        // The app never cancels a booked appointment itself: the coordinator does, once the patient's been told.
        foreach (var booked in _appointments.Where(a => a.Status == AppointmentStatus.Booked && a.StartUtc > at).ToList())
        {
            var stage = _pathway.Stages.FirstOrDefault(s => s.Id == booked.StageId);
            CancelAppointment(booked, stage, _world.Coordinator, at.AddMinutes(_random.Next(10, 90)), NoteBuilder.PathwayCancelledAppointmentReason);
        }
    }

    /// <summary>Applies one request's change at a moment, as a handler would: audit, visits, the monitor's notifications.</summary>
    private void Change(StaffMember who, DateTime at, Action<PathwayChange> apply)
    {
        var change = new PathwayChange(_pathway, who.Actor, at);
        apply(change);
        _audit.AddRange(change.AuditEntries);
        _noteVersions.AddRange(change.NoteVersions);
        _appointments.AddRange(change.NewAppointments);
        _events.AddRange(change.AppointmentEvents);
        // The clock's own history (stopped, paused, resumed, target re-read), saved with the change (its pauses go
        // with the referral).
        _referralEvents.AddRange(change.ReferralEvents);
        _grants.AddRange(change.NewGrants);
        Touch(who, at);

        if (at >= _world.Timeline.NotificationsFromUtc)
            _notifications.AddRange(_world.Monitor(_pathway, change.AuditEntries, who.Id, at));
    }

    /// <summary>Assigns someone, as AssignPatientPathwayHandler does: they're told, unless they assigned themselves.</summary>
    private void Assign(StaffMember admin, DateTime at, StaffMember assignee)
    {
        if (_pathway.Assignments.Any(a => a.UserId == assignee.Id))
            return;

        var change = new PathwayChange(_pathway, admin.Actor, at);
        change.Assign(assignee.Id, assignee.Username);
        _audit.AddRange(change.AuditEntries);
        Touch(admin, at);

        if (at < _world.Timeline.NotificationsFromUtc)
            return;

        if (AssignmentNotifications.For(_pathway, assignee.Id, admin.Actor, at) is { } notification && _world.Delivers(notification, at))
            _notifications.Add(notification);

        _notifications.AddRange(_world.Monitor(_pathway, change.AuditEntries, admin.Id, at));
    }

    /// <summary>
    /// A details edit, as UpdatePatientPathwayHandler makes and records it (<see cref="PathwayDetails"/>): never the
    /// target date while the referral's clock sets it (the handler refuses it), and a new priority re-reads the clock's
    /// target.
    /// </summary>
    private void EditDetails(StaffMember who, DateTime at, DateOnly? due = null, Priority? priority = null)
    {
        if (at >= _now || !PathwayWorkflow.CanEditDetails(_pathway))
            return;
        if (due is not null && WaitingClock.SetsPathwayTarget(_pathway.Referral))
            return;

        var changes = PathwayDetails.Apply(_pathway, null, due, priority);
        if (changes.Count > 0)
            Change(who, at, change =>
            {
                PathwayDetails.Record(change, changes);
                if (changes.ContainsKey(PathwayDetails.PriorityField))
                    change.PriorityChanged();
            });
    }

    /// <summary>
    /// The pathway's first note updated by its author (as EditPathwayNoteHandler does: only the author can), now and
    /// then corrected again the same day, so it has several versions; with no first note, one added by
    /// <paramref name="worker"/>.
    /// </summary>
    private void UpdateFirstNote(StaffMember worker, DateTime at)
    {
        if (at >= _now || !PathwayWorkflow.CanChangeNotes(_pathway))
            return;

        var first = _pathway.Notes.OrderBy(n => n.CreatedAtUtc).FirstOrDefault();
        var author = first?.AuthorUserId is { } authorId ? _world.Staff.FirstOrDefault(s => s.Id == authorId) : null;
        if (first is null || author is null)
        {
            Change(worker, at, change => change.AddNote(NoteBuilder.PathwayNoteUpdate(string.Empty, _random)));
            return;
        }

        Change(author, at, change => change.EditNote(first, NoteBuilder.PathwayNoteUpdate(first.Text, _random)));
        var again = at.AddMinutes(_random.Next(20, 240));
        if (again < _now && _random.NextDouble() < 0.3)
            Change(author, again, change => change.EditNote(first, NoteBuilder.PathwayNoteUpdate(first.Text, _random)));
    }

    /// <summary>
    /// Now and then, after finishing a stage, its worker adds a note to the pathway about what comes next (as
    /// AddPathwayNoteHandler does), and sometimes fixes it a little later: their own, so they can.
    /// </summary>
    private void MaybePathwayNote(StageSpec spec, PatientPathwayStage stage, StaffMember worker, DateTime finishedAt)
    {
        var at = finishedAt.AddMinutes(_random.Next(2, 45));
        if (at >= _now || _random.NextDouble() >= 0.22 || !PathwayWorkflow.CanChangeNotes(_pathway))
            return;

        PatientPathwayNote? added = null;
        Change(worker, at, change => added = change.AddNote(NoteBuilder.PathwayFollowUpNote(stage.Name, spec.Role, _random)));

        var fixAt = at.AddMinutes(_random.Next(5, 120));
        if (added is not null && fixAt < _now && _random.NextDouble() < 0.15)
            Change(worker, fixAt, change => change.EditNote(added, NoteBuilder.PathwayNoteFix(added.Text, _random)));
    }

    private void MaybeProgressNote(PatientPathwayStage stage, StaffMember worker, DateTime startedAt, NoteContext context)
    {
        var at = startedAt.AddMinutes(_random.Next(5, 90));
        if (at < _now && _random.NextDouble() < 0.5 && PathwayWorkflow.CanEditStage(_pathway, stage))
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
    private void Book(Appointment appointment, PatientPathwayStage? stage, StaffMember by, DateTime at, StaffMember staff, ScheduleAllocator.Slot slot)
    {
        var previousStatus = appointment.Status;
        var previousStaff = appointment.StaffUserId;
        var previousStart = appointment.StartUtc;
        var change = new AppointmentChange(appointment, by.Actor, at,
            previousStaff is { } id ? _world.UsernameOf(id) : null, _pathway.Title, stage?.Name);
        change.Book(staff.Id, staff.Username, slot.StartUtc, slot.EndUtc);
        _events.AddRange(change.Events);
        Touch(by, at);

        if (at >= _world.Timeline.NotificationsFromUtc)
            _notifications.AddRange(AppointmentNotifications.AfterBooking(appointment, previousStatus, previousStaff, previousStart, by.Actor, at));
    }

    /// <summary>A new appointment booked straight away, as CreateAppointmentHandler does when given a time.</summary>
    private Appointment NewBooked(PatientPathwayStage stage, StaffMember by, DateTime at, StaffMember staff, ScheduleAllocator.Slot slot)
    {
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = _pathway.PatientId,
            TeamId = _pathway.TeamId,
            PathwayId = _pathway.Id,
            StageId = stage.Id,
            Status = AppointmentStatus.Requested,
            IsAutomatic = false,
            CreatedAtUtc = at,
            CreatedByUserId = by.Id,
            LastUpdated = at,
        };
        var change = new AppointmentChange(appointment, by.Actor, at, pathwayTitle: _pathway.Title, stageName: stage.Name);
        change.Book(staff.Id, staff.Username, slot.StartUtc, slot.EndUtc);
        _appointments.Add(appointment);
        _events.AddRange(change.Events);
        Touch(by, at);

        if (at >= _world.Timeline.NotificationsFromUtc)
            _notifications.AddRange(AppointmentNotifications.AfterBooking(appointment, AppointmentStatus.Requested, null, null, by.Actor, at));
        return appointment;
    }

    private void CancelAppointment(Appointment appointment, PatientPathwayStage? stage, StaffMember by, DateTime at, string reason)
    {
        var previous = appointment.Status;
        var change = new AppointmentChange(appointment, by.Actor, at, StaffName(appointment), _pathway.Title, stage?.Name);
        change.Cancel(reason);
        _events.AddRange(change.Events);
        if (previous == AppointmentStatus.Booked)
            Release(appointment);
        Touch(by, at);

        if (at >= _world.Timeline.NotificationsFromUtc)
            _notifications.AddRange(AppointmentNotifications.AfterCancelling(appointment, previous, by.Actor, at));
    }

    private void RecordOutcome(Appointment appointment, PatientPathwayStage stage, StaffMember by, DateTime at, AppointmentStatus outcome)
    {
        var change = new AppointmentChange(appointment, by.Actor, at, StaffName(appointment), _pathway.Title, stage.Name);
        change.RecordOutcome(outcome);
        _events.AddRange(change.Events);
        Touch(by, at);
    }

    private void UpdateNotes(Appointment appointment, PatientPathwayStage stage, StaffMember by, DateTime at, string notes)
    {
        if (at >= _now)
            return;
        var change = new AppointmentChange(appointment, by.Actor, at, StaffName(appointment), _pathway.Title, stage.Name);
        change.UpdateNotes(notes);
        _events.AddRange(change.Events);
    }

    private string? StaffName(Appointment appointment) =>
        appointment.StaffUserId is { } id ? _world.UsernameOf(id) : null;

    // People and times -------------------------------------------------------------------------------------

    private StaffMember WorkerFor(StageSpec spec) => spec.Role switch
    {
        StageRole.Coordinator => _world.Coordinator,
        StageRole.Lab => _world.Member(DemoStaff.ScientistUsername),
        StageRole.Samples => _samplesTaker,
        StageRole.Nurse => _followUpNurse,
        StageRole.Pharmacist => _world.Member(DemoStaff.PharmacistUsername),
        _ => _lead,
    };

    private void Touch(StaffMember who, DateTime at) =>
        _touches.Add(new Touch(who.Id, at, _pathway.PatientId, _pathway.Id));

    private DateTime Working(DateTime utc) => DemoTimeline.InWorkingHours(Max(utc, _floor), _random);

    private DateTime Later(DateTime utc) => Max(utc, _floor);

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
}
