using MedApp.Application.Appointments.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Specimens.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Consent;
using MedApp.Domain.Mdt;
using MedApp.Domain.Results;
using MedApp.Domain.Specimens;
using MedApp.Domain.Calendar;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Referrals;
using AuditAction = MedApp.Domain.Pathways.PatientPathways.PatientPathwayAuditAction;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// Records to open when showing the app (see <c>docs/demo-data.md</c>): the names and titles depend on the day the
/// seed runs, so the summary lists them. Patients are named as "First Last", which no other patient shares (see
/// <see cref="DemoPatients"/>), so searching for the name finds the one record; pathways as "Title, for First Last";
/// referrals as "Specialty referral for First Last, received 3 November 2026" (a patient can have two to one specialty).
/// </summary>
public static class DemoTour
{
    public static object Build(DemoWorld world, IReadOnlyList<PathwayPlan> plans, IReadOnlyList<PatientSeed> patients, IReadOnlyList<ReferralStory> stories,
        IReadOnlyDictionary<string, Guid> letters, object clinics)
    {
        var names = patients.ToDictionary(p => p.Id, p => p.FullName);
        var deleted = world.Deletions.Select(d => d.Pathway.Id).ToHashSet();
        var byId = world.Pathways.Where(p => !deleted.Contains(p.Id)).ToDictionary(p => p.Id);
        string Describe(PatientPathway p) => $"{p.Title}, for {names[p.PatientId]}";
        IEnumerable<string> Pathways(Func<PathwayPlan, bool> where, Func<PatientPathway, bool>? also = null, int take = 3) =>
            plans.Where(where).Select(p => byId.GetValueOrDefault(p.PathwayId)).OfType<PatientPathway>()
                .Where(p => also?.Invoke(p) ?? true).Take(take).Select(Describe);

        var audit = world.Audit.ToLookup(a => a.PatientPathwayId);
        bool Has(PatientPathway p, AuditAction action, string? detail = null) =>
            audit[p.Id].Any(a => a.Action == action && (detail is null || a.Details.Contains(detail)));

        var lead = world.Lead;
        var day = world.Timeline.DiaryDay;
        return new
        {
            blocked = Pathways(p => p.Fate == Fate.Blocked, p => p.Status == Status.Blocked),
            blockedThenUnblocked = Pathways(p => p.Fate == Fate.Unblocked, p => Has(p, AuditAction.StatusChanged, "\"from\":\"Blocked\"")),
            cancelledWithItsBookingCancelled = Pathways(p => p.Fate == Fate.CancelledAfterBooking, p => p.Status == Status.Cancelled),
            stageUnlockedAndCorrected = Pathways(p => p.UnlockStage >= 0, p => Has(p, AuditAction.StageUnlocked)),
            finishedWithoutItsVisit = Pathways(_ => true, p => Has(p, AuditAction.StageFinished, "\"visitRequirement\":\"Required\"")),
            dueDateChangedByHand = Pathways(p => p.EditDueDate, p => audit[p.Id].Any(a => a.Action == AuditAction.DetailsUpdated
                                                                                         && a.Details.Contains("\"dueDate\"") && !a.Details.Contains("\"source\":\"clock\""))),
            reassigned = Pathways(p => p.ReassignStage >= 0, p => Has(p, AuditAction.Unassigned)),
            visitNeverBooked = Pathways(p => p.NeverBookStage >= 0, p => PathwayWorkflow.IsOpen(p.Status)),
            oldPathwayStillFinishing = Pathways(p => p.Straggler, p => PathwayWorkflow.IsOpen(p.Status)),
            missedAndRebooked = world.Appointments.Where(a => a.Status == AppointmentStatus.DidNotAttend && a.PathwayId is { } id && byId.ContainsKey(id))
                .Select(a => Describe(byId[a.PathwayId!.Value])).Distinct().Take(3),
            inReview = byId.Values.Where(p => p.Status == Status.InReview).Take(3).Select(Describe),
            deletedPathwaysOf = world.Deletions.Select(d => names[d.Pathway.PatientId]),
            leadsDiaryDay = day,
            leadsAppointmentsThatDay = world.Appointments
                .Where(a => a.StaffUserId == lead.Id && a.StartUtc is { } s && UkTime.DateOf(s) == day && AppointmentWorkflow.IsOnCalendar(a.Status))
                .OrderBy(a => a.StartUtc).Select(a => $"{UkTime.DescribeTime(a.StartUtc!.Value)} {names[a.PatientId]}"),
            calendar = Calendar(world),
            referrals = Referrals(world, plans, stories, names, byId),
            centre = Centre(world, names, byId, letters, clinics),
        };
    }

    /// <summary>
    /// Where to see Phases 3 to 5 (the lab, results, consent, the MDT list, branches, letters, clinic sessions): each
    /// special case the story made, by its pathway ("Title, for First Last"), or a specimen by its number.
    /// </summary>
    private static object Centre(DemoWorld world, IReadOnlyDictionary<Guid, string> names, IReadOnlyDictionary<Guid, PatientPathway> byId,
        IReadOnlyDictionary<string, Guid> letters, object clinics)
    {
        var now = world.Timeline.NowUtc;
        var types = world.SpecimenSetup.Types.ToDictionary(t => t.Id);
        string? Of(Guid? pathwayId) => pathwayId is { } id && byId.TryGetValue(id, out var p) ? $"{p.Title}, for {names[p.PatientId]}" : null;
        string Specimen(Specimen s) => $"{s.Number} ({types[s.SpecimenTypeId].Name}): {Of(s.PathwayId)}";
        IEnumerable<string> Branch(string branch) => world.Audit
            .Where(a => a.Action == AuditAction.BranchChosen && a.Details.Contains($"\"branch\":\"{branch}\""))
            .OrderByDescending(a => a.AtUtc).Select(a => Of(a.PatientPathwayId)).OfType<string>().Take(2);
        var nextMdt = world.MdtMeetings.FirstOrDefault(m => m.StartUtc > now);
        var lastMdt = world.MdtMeetings.LastOrDefault(m => m.StartUtc <= now);
        var referralLetter = letters.TryGetValue("informationRequestDraft", out var referralId)
            ? world.Referrals.FirstOrDefault(r => r.Id == referralId) : null;

        return new
        {
            labAndLettersFrom = UkTime.DateOf(world.Timeline.LabAndLettersFromUtc),
            specimenRejectedAndTakenAgain = world.Specimens.Where(s => s.Status == SpecimenStatus.RejectedByLab).Select(Specimen),
            specimenLostAndPathwayOnHold = world.Specimens.Where(s => s.Status == SpecimenStatus.Lost).Select(Specimen),
            specimensOverdueAtLab = world.Specimens
                .Where(s => SpecimenWorkflow.IsOpen(s.Status) && s.TakenAtUtc.AddDays(types[s.SpecimenTypeId].ExpectedTurnaroundDays) < now)
                .OrderByDescending(s => s.TakenAtUtc).Take(3).Select(Specimen),
            resultEnteredInErrorAndReportedAgain = world.Results.Where(r => r.Status == ResultStatus.EnteredInError).Select(r => Of(r.PathwayId)),
            resultsAwaitingSignOff = world.Results.Count(r => r.IsAwaitingSignOff),
            resultsAwaitingOverTwoWeeks = world.Results.Where(r => r.IsAwaitingSignOff && r.ReportedAtUtc < now.AddDays(-14))
                .Select(r => Of(r.PathwayId) ?? $"{names[r.PatientId]} (on the patient's record)"),
            resultNeedingAClinician = world.Results.Where(r => r.Source == ResultSource.ExternalReport).Select(r => names[r.PatientId]),
            consentWithdrawnAndPathwayOnHold = world.ConsentRecords.Where(c => c.Status == ConsentStatus.Withdrawn).Select(c => Of(c.PathwayId)),
            mdt = new
            {
                lastMeeting = lastMdt is null ? null : UkTime.Describe(lastMdt.StartUtc),
                discussedThere = lastMdt is null ? 0 : world.MdtItems.Count(i => i.MeetingId == lastMdt.Id && i.Status == MdtItemStatus.Discussed),
                nextMeeting = nextMdt is null ? null : UkTime.Describe(nextMdt.StartUtc),
                listedForIt = nextMdt is null ? [] : world.MdtItems.Where(i => i.MeetingId == nextMdt.Id && i.Status == MdtItemStatus.Listed).Select(i => Of(i.PathwayId)),
            },
            benignBranchChosen = Branch(DemoPathwayKinds.BenignBranch),
            treatmentBranchChosen = Branch(DemoPathwayKinds.TreatmentBranch),
            stageSkippedPatientDeclined = world.Audit.Where(a => a.Action == AuditAction.StageSkipped).Select(a => Of(a.PatientPathwayId)).OfType<string>().Distinct().Take(3),
            stageSkippedThenReinstated = world.Audit.Where(a => a.Action == AuditAction.StageReinstated).Select(a => Of(a.PatientPathwayId)),
            didNotAttendAwaitingDecision = world.Appointments
                .Where(a => a.Status == AppointmentStatus.DidNotAttend && a.DnaFollowUp is null && a.EndUtc >= world.Timeline.LabAndLettersFromUtc)
                .Select(a => Of(a.PathwayId)).OfType<string>(),
            letters = letters.Where(l => l.Key != "informationRequestDraft").ToDictionary(l => l.Key, l => Of(l.Value)),
            informationRequestDraftedOn = referralLetter is null ? null
                : $"{world.Specialties[referralLetter.SpecialtyId].Name} referral for {names[referralLetter.PatientId]}",
            clinics,
        };
    }

    /// <summary>
    /// Where to see referrals, triage and the waiting-time clock: the inbox's breached referrals, the new service nobody
    /// can triage, a referral moved here and a confirmed duplicate awaiting triage; one closed each way; the clocks
    /// paused (one past its review date), resumed and breached; and pathways made without a referral.
    /// </summary>
    private static object Referrals(DemoWorld world, IReadOnlyList<PathwayPlan> plans, IReadOnlyList<ReferralStory> stories,
        IReadOnlyDictionary<Guid, string> names, IReadOnlyDictionary<Guid, PatientPathway> pathways)
    {
        var today = world.Timeline.Today;
        var specialty = world.Specialties.ToDictionary(s => s.Key, s => s.Value.Name);
        var events = world.ReferralEvents.ToLookup(e => e.ReferralId);
        string Referral(Referral r) => $"{specialty[r.SpecialtyId]} referral for {names[r.PatientId]}, received {r.ReceivedOn.ToString("d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture)}";
        string Pathway(PatientPathway p) => $"{p.Title}, for {names[p.PatientId]}";
        IEnumerable<string> Pathways(Func<Referral, bool> where, int take = 3) =>
            world.Referrals.Where(r => r.PathwayId is { } id && pathways.ContainsKey(id) && where(r))
                .Take(take).Select(r => Pathway(pathways[r.PathwayId!.Value]));
        IEnumerable<string> Closed(ReferralStatus status, int take = 2) =>
            world.Referrals.Where(r => r.Status == status).OrderByDescending(r => r.DecidedAtUtc).Take(take).Select(Referral);

        var awaiting = world.Referrals.Where(r => r.Status == ReferralStatus.AwaitingTriage).ToList();
        var newService = world.Specialty(DemoSpecialties.NewService);
        var duplicate = stories.FirstOrDefault(s => s is { Duplicate: true, Outcome: StoryOutcome.AwaitingTriage })?.Referral;
        var duplicateRejected = stories.FirstOrDefault(s => s is { Duplicate: true, Outcome: StoryOutcome.Rejected })?.Referral;
        var wrongPatient = stories.FirstOrDefault(s => s.Outcome == StoryOutcome.EnteredInError)?.Referral;

        return new
        {
            awaitingTriage = awaiting.Count,
            awaitingTriageBreached = awaiting.Where(r => WaitingClock.IsBreached(r, today)).OrderBy(r => r.ReceivedOn).Select(Referral),
            nobodyCanTriage = new { specialty = newService.Name, referrals = awaiting.Where(r => r.SpecialtyId == newService.Id).Select(Referral) },
            redirectedHereAwaitingTriage = awaiting.Where(r => events[r.Id].Any(e => e.Action == ReferralEventAction.SpecialtyChanged)).Select(Referral),
            duplicateAwaitingTriage = duplicate is null ? null : Referral(duplicate),
            registerAgainToSeeTheDuplicateWarning = duplicate is null ? null : $"{names[duplicate.PatientId]}, in {specialty[duplicate.SpecialtyId]}",
            acceptedAfterBeingRedirected = Pathways(r => events[r.Id].Any(e => e.Action == ReferralEventAction.SpecialtyChanged)),
            raisedToUrgentAtTriage = Pathways(r => r.RequestedPriority != PatientPathwayPriority.Urgent
                                                   && pathways[r.PathwayId!.Value].Priority == PatientPathwayPriority.Urgent
                                                   && plans.Any(p => p.PathwayId == r.PathwayId && p.RaisePriority)),
            rejected = Enum.GetValues<ReferralRejectionReason>().ToDictionary(reason => reason.ToString(), reason =>
                world.Referrals.Where(r => r.Status == ReferralStatus.Rejected && r.RejectionReason == reason)
                    .OrderByDescending(r => r.DecidedAtUtc).Select(Referral).FirstOrDefault()),
            duplicateRejected = duplicateRejected is null ? null : Referral(duplicateRejected),
            redirectedElsewhere = Closed(ReferralStatus.RedirectedElsewhere),
            withdrawn = Closed(ReferralStatus.Withdrawn),
            enteredInError = wrongPatient is null ? null : Referral(wrongPatient),
            clocks = new
            {
                paused = Pathways(r => WaitingClock.IsPaused(r) && !WaitingClock.IsReviewOverdue(r, today), take: 4),
                pauseReviewOverdue = Pathways(r => WaitingClock.IsReviewOverdue(r, today)),
                resumed = Pathways(r => events[r.Id].Any(e => e.Action == ReferralEventAction.ClockResumed && !e.Details.Contains("\"automatic\":true"))),
                breachedOpen = Pathways(r => !WaitingClock.IsStopped(r) && WaitingClock.IsBreached(r, today)
                                             && PathwayWorkflow.IsOpen(pathways[r.PathwayId!.Value].Status)),
                breachedStopped = Pathways(r => WaitingClock.IsStopped(r) && WaitingClock.IsBreached(r, today)
                                                && r.StopReason == ClockStopReason.StopStageFinished),
                createdWithoutAReferral = plans.Where(p => !p.FromReferral && pathways.ContainsKey(p.PathwayId))
                    .Select(p => Pathway(pathways[p.PathwayId])).Take(3),
            },
        };
    }

    /// <summary>
    /// Where to see meetings, the team view and personal templates: the diary day's meetings (open Calendar, Team, on
    /// that day), the next few meetings, the ones moved or cancelled (their notifications are in the bell), and whose
    /// templates differ from the usual.
    /// </summary>
    public static object Calendar(DemoWorld world)
    {
        var calendars = world.Calendars;
        var plan = calendars.MeetingPlan;
        var now = world.Timeline.NowUtc;
        var day = world.Timeline.DiaryDay;
        string Describe(Meeting m) => $"{UkTime.Describe(m.StartUtc)} to {UkTime.DescribeTime(m.EndUtc)}: {m.Title} ({m.Attendees.Count} going)";

        return new
        {
            teamViewDay = day,
            meetingsThatDay = calendars.Meetings.Where(m => UkTime.DateOf(m.StartUtc) == day).OrderBy(m => m.StartUtc).Select(Describe),
            upcomingMeetings = calendars.Meetings.Where(m => m.StartUtc > now).OrderBy(m => m.StartUtc).Take(8).Select(Describe),
            movedMeetings = new[] { plan.Replanned, plan.Moved, plan.MovedOneToOne }.OfType<MeetingStory>().Select(s => Describe(s.Meeting)),
            cancelledMeeting = plan.Cancelled is { } cancelled ? Describe(cancelled.Meeting) : null,
            personalTemplates = new[]
            {
                $"{DemoStaff.DiabetesNurseUsername}: '{DemoTemplates.PartTimeWeek}' since {UkTime.DescribeDate(world.Timeline.ThisMonday.AddDays(-7 * 20))}, '{DemoTemplates.FullTimeWeek}' before",
                $"{DemoTemplates.LateLunchUsername}: '{DemoTemplates.MinorSurgeryList}' lunch at 13:15 since {UkTime.DescribeDate(world.Timeline.ThisMonday.AddDays(-7 * 10))}, 12:45 before",
                $"{DemoStaff.GynaecologistUsername} and {DemoStaff.UrologistUsername}: a 'Visiting week' ('{DemoTemplates.AtTheHospital}' on the days they aren't here)",
                $"{DemoStaff.ScientistUsername}: '{DemoTemplates.LabReporting}' every day",
                $"{DemoStaff.CoordinatorUsername}: a late lunch, 13:30, covering the desk",
                $"{DemoStaff.SkinGpUsername}: '{DemoTemplates.FridayList}' (minor surgery on Friday mornings)",
                $"{DemoStaff.ManagerUsername}: '{DemoTemplates.OfficeEarlyFinish}' on Fridays",
                $"{world.Lead.Username}: '{DemoTemplates.ClinicAndManagement}' on Wednesdays, '{DemoTemplates.OutreachClinic}' on Thursdays",
            },
        };
    }
}
