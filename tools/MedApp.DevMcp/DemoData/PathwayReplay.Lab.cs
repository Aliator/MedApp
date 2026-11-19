using MedApp.Domain.Teams;
using System.Security.Cryptography;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Common.Identity;
using MedApp.Application.Consent.Common;
using MedApp.Application.Mdt.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Results.Common;
using MedApp.Application.Specimens.Commands.ReportSpecimen;
using MedApp.Application.Specimens.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Attachments;
using MedApp.Domain.Calendar;
using MedApp.Domain.Consent;
using MedApp.Domain.Letters;
using MedApp.Domain.Mdt;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Results;
using MedApp.Domain.Specimens;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// A letter a replay planned: written (and approved and sent) through the app's own letter commands once the pathways
/// are saved (<see cref="DemoLetters"/>), at the moments planned here. A step at or after now hasn't happened yet.
/// </summary>
public sealed record LetterPlan(
    Guid PathwayId,
    Guid PatientId,
    string Team,
    LetterKind Kind,
    StaffMember Author,
    DateTime WrittenUtc,
    string Body,
    StaffMember? Approver,
    DateTime? SubmittedUtc,
    DateTime? ApprovedUtc,
    StaffMember Sender,
    DateTime? SentUtc)
{
    /// <summary>Returned once by the approver with a comment, corrected and submitted again.</summary>
    public DateTime? ReturnedUtc { get; init; }

    /// <summary>Withdrawn by its author after it was sent, and a correction written, approved and sent.</summary>
    public DateTime? WithdrawnUtc { get; init; }
}

/// <summary>
/// Phases 3 to 5 in a pathway's story (<c>docs/centre-phases-3-5-plan.md</c>, sections 2.7, 3.9 and 4.13), as the
/// app's shared code makes them: consent recorded before a biopsy (<see cref="ConsentChange"/>); specimens taken at the
/// visit, sent, received and reported (<see cref="SpecimenChange"/>, <see cref="SpecimenWorkflow"/>); the result each
/// report makes, signed off by the requesting clinician with what they did (<see cref="ResultChange"/>); skin lesions
/// listed for the skin cancer team's MDT and their outcomes recorded (<see cref="MdtAudit"/>); the branch a clinician
/// chooses after the results; optional stages skipped when a patient declines; and the discharge summary a discharge
/// letter stage needs, planned here and written through the letter commands later. Only from
/// <see cref="DemoTimeline.LabAndLettersFromUtc"/>: before then the centre did these on paper.
/// <para>
/// Nothing clinical is decided here: the actions, outcomes and letters are the text staff "wrote" in the story, from
/// the referral's planned outcome. MedApp never reads a report or suggests an action.
/// </para>
/// </summary>
public sealed partial class PathwayReplay
{
    private readonly List<Specimen> _specimens = [];
    private readonly List<Result> _results = [];
    private readonly List<ConsentRecord> _consents = [];
    private readonly List<MdtItem> _mdtItems = [];
    private readonly List<Attachment> _attachments = [];
    private readonly Dictionary<Guid, byte[]> _files = [];
    private LetterPlan? _letter;

    private bool LabAndLetters(DateTime utc) => utc >= _world.Timeline.LabAndLettersFromUtc;

    private bool IsSkin => _plan.PathwayKind is PathwayKind.SkinLesion or PathwayKind.SkinLesion2024;

    /// <summary>The stage rules that came with Phases 3 to 5, copied onto pathways created from then only.</summary>
    private void ApplyStageRulesOfTheTime()
    {
        if (LabAndLetters(_plan.CreatedUtc))
            return;
        foreach (var stage in _stages)
        {
            stage.NeedsLetterKind = null;
            stage.WaitsForResults = false;
            stage.NeedsConsent = false;
        }
    }

    /// <summary>The branch the clinician chose at a branch point: treatment if the outcome had one, otherwise benign.</summary>
    private string? BranchFor(PatientPathwayStage stage) =>
        !stage.ChoosesBranch ? null
        : _plan.NoteContext().Result.Treatment.Length > 0 ? DemoPathwayKinds.TreatmentBranch
        : DemoPathwayKinds.BenignBranch;

    // Consent -------------------------------------------------------------------------------------------------

    /// <summary>Records the patient's signed consent form on the stage (as RecordConsentHandler does).</summary>
    private ConsentRecord RecordConsent(PatientPathwayStage stage, StaffMember by, DateTime at)
    {
        if (ConsentWorkflow.WhyCantRecord(_pathway, stage) is { } why)
            throw new InvalidOperationException($"Seeding went wrong: {why}");

        var gynaecology = _plan.PathwayKind == PathwayKind.PostMenopausalBleeding;
        var record = new ConsentRecord
        {
            Id = Guid.NewGuid(),
            PatientId = _pathway.PatientId,
            TeamId = _pathway.TeamId,
            PathwayId = _pathway.Id,
            StageId = stage.Id,
            ConsentFormId = gynaecology ? _world.Setup.GynaecologyConsentFormId : _world.Setup.SkinConsentFormId,
            FormName = gynaecology ? DemoCentreSetup.GynaecologyConsentForm : DemoCentreSetup.SkinConsentForm,
            FormVersion = "v1 (demo)",
            SignedOn = DueDates.ToUkDate(at),
            SignedBy = ConsentSignedBy.Patient,
            RecordedByUserId = by.Id,
            RecordedAtUtc = at,
        };
        var change = new ConsentChange(record, _pathway, by.Actor, at);
        change.Recorded();
        _audit.AddRange(change.AuditEntries);
        _consents.Add(record);
        Touch(by, at);
        return record;
    }

    /// <summary>
    /// The one whose patient withdrew consent before the biopsy: recorded when the stage started, withdrawn days later,
    /// and the pathway put on hold while they think it over. Returns whether the story stops here.
    /// </summary>
    private bool ConsentWithdrawn(PatientPathwayStage stage, StaffMember worker, DateTime startAt)
    {
        if (!_plan.WithdrawsConsent || stage.Name != DemoPathwayKinds.BiopsyOrSample || !LabAndLetters(startAt))
            return false;

        var recordedAt = startAt.AddMinutes(_random.Next(10, 60));
        var withdrawAt = Working(recordedAt.AddDays(2 + _random.NextDouble() * 3));
        if (withdrawAt >= _now)
            return false;

        var record = RecordConsent(stage, worker, recordedAt);
        var change = new ConsentChange(record, _pathway, _lead.Actor, withdrawAt);
        change.Withdraw(DueDates.ToUkDate(withdrawAt), "The patient rang: they've decided not to go ahead for now and want time to think it over.");
        _audit.AddRange(change.AuditEntries);
        Touch(_lead, withdrawAt);
        Change(_lead, withdrawAt.AddMinutes(_random.Next(3, 20)), c => c.ChangeStatus(Status.Blocked,
            "Consent withdrawn before the biopsy: the patient is thinking it over. Ring back in two weeks."));
        return true;
    }

    // Specimens -----------------------------------------------------------------------------------------------

    private static bool TakesSpecimens(PatientPathwayStage stage) =>
        stage.Name is DemoPathwayKinds.BiopsyOrSample or DemoPathwayKinds.BloodAndUrineTests;

    /// <summary>
    /// What the visit took (as RecordSpecimenHandler records each): consent for a biopsy first, then the biopsy (to the
    /// in-house lab, or now and then the partner lab) or the blood and urine samples, each sent within the hour or so.
    /// For one, the lab rejects the unlabelled urine pot and it's taken again before the patient leaves. Returns the
    /// last moment anything was recorded.
    /// </summary>
    private DateTime TakeSpecimens(PatientPathwayStage stage, StaffMember worker, DateTime visitEndedAt)
    {
        var at = visitEndedAt.AddMinutes(1);
        if (SpecimenWorkflow.WhyCantTake(_pathway, stage) is { } why)
            throw new InvalidOperationException($"Seeding went wrong: {why}");

        var biopsy = stage.Name == DemoPathwayKinds.BiopsyOrSample;
        if (biopsy)
            RecordConsent(stage, worker, at.AddMinutes(-0.5));

        var setup = _world.SpecimenSetup;
        var taken = new List<Specimen>();
        if (biopsy)
        {
            var type = _plan.PathwayKind == PathwayKind.PostMenopausalBleeding ? setup.Type("Endometrial biopsy")
                : _plan.Kind.Sample.Contains("punch", StringComparison.OrdinalIgnoreCase) ? setup.Type("Skin biopsy (punch)")
                : setup.Type("Skin excision");
            var lab = _plan.LosesSpecimen || (!_plan.ResultInError && !_plan.SlowSignOff && _random.NextDouble() < 0.2) ? setup.Partner : setup.InHouse;
            taken.Add(Take(stage, worker, type, lab, visitEndedAt.AddMinutes(-5), at, _plan.Side, $"{Capital(_plan.Kind.Sample)}. Clinical details on the request form."));
        }
        else
        {
            taken.Add(Take(stage, worker, setup.Type("Blood"), setup.InHouse, visitEndedAt.AddMinutes(-6), at, null, $"{Capital(_plan.Kind.Sample)}, as on the request."));
            taken.Add(Take(stage, worker, setup.Type("Urine"), setup.InHouse, visitEndedAt.AddMinutes(-4), at.AddMinutes(1), null, "Mid-stream sample."));
        }

        var last = at.AddMinutes(1);
        var sendAt = at.AddMinutes(_random.Next(20, 90));
        if (sendAt >= _now)
            return last;
        foreach (var specimen in taken)
            Send(specimen, worker, sendAt);
        last = sendAt;

        // The lab's booking-in finds the urine pot unlabelled: rejected, and taken again while the patient is still here.
        if (_plan.RejectsSpecimen && !biopsy)
        {
            var urine = taken.Single(s => s.SpecimenType.Name == "Urine");
            var scientist = _world.Member(DemoStaff.ScientistUsername);
            var receiveAt = sendAt.AddMinutes(_random.Next(15, 30));
            var rejectAt = receiveAt.AddMinutes(_random.Next(3, 10));
            var retakeAt = rejectAt.AddMinutes(_random.Next(15, 30));
            if (retakeAt.AddMinutes(30) >= _now)
                return last;

            Lab(urine, scientist, receiveAt, c => c.Receive(receiveAt, LabReference()));
            Lab(urine, scientist, rejectAt, c => c.Reject(SpecimenRejection.Reason, SpecimenRejection.Detail));
            Tell(SpecimenNotifications.Rejected(urine, scientist.Actor, rejectAt), rejectAt);

            var again = Take(stage, worker, urine.SpecimenType, urine.Laboratory, retakeAt, retakeAt.AddMinutes(2), null,
                "Mid-stream sample, taken again: the first pot came without a label.");
            var resendAt = retakeAt.AddMinutes(_random.Next(10, 25));
            Send(again, worker, resendAt);
            last = resendAt;
        }

        return last;
    }

    private Specimen Take(PatientPathwayStage stage, StaffMember by, SpecimenType type, Laboratory lab, DateTime takenAt, DateTime recordedAt,
        string? site, string notes)
    {
        var specimen = new Specimen
        {
            Id = Guid.NewGuid(),
            Number = SpecimenNumbers.Format(new SpecimenOptions().NumberPrefix, recordedAt, _world.NextSpecimenNumber++),
            PatientId = _pathway.PatientId,
            TeamId = _pathway.TeamId,
            PathwayId = _pathway.Id,
            StageId = stage.Id,
            SpecimenTypeId = type.Id,
            SpecimenType = type,
            LaboratoryId = lab.Id,
            Laboratory = lab,
            Site = site,
            RequestNotes = notes,
            RequestingUserId = _lead.Id,
            TakenAtUtc = takenAt,
            TakenByUserId = by.Id,
            RecordedAtUtc = recordedAt,
        };
        var change = new SpecimenChange(specimen, _pathway, by.Actor, recordedAt);
        change.Take();
        _audit.AddRange(change.AuditEntries);
        // Passed to the lab: it sees the pathway in full from then on, and keeps it (the team walls).
        if (!_passedToLab)
        {
            change.PassToLab(Team.LabId);
            _passedToLab = true;
        }
        _grants.AddRange(change.NewGrants);
        _specimens.Add(specimen);
        Touch(by, recordedAt);
        return specimen;
    }

    private void Send(Specimen specimen, StaffMember by, DateTime at) =>
        Lab(specimen, by, at, c => c.Send(at, specimen.Laboratory.UsesMedApp ? null : $"CR{_random.Next(100000, 999999)}"));

    /// <summary>One step of a specimen's life, as its handler applies it: checked by the rules first.</summary>
    private void Lab(Specimen specimen, StaffMember by, DateTime at, Action<SpecimenChange> apply)
    {
        var change = new SpecimenChange(specimen, _pathway, by.Actor, at);
        apply(change);
        _audit.AddRange(change.AuditEntries);
        Touch(by, at);
    }

    private string LabReference() => $"L{_random.Next(10000, 99999)}";

    /// <summary>
    /// The lab's stage, for a pathway whose specimens are still out: received when the lab books them in, reported when
    /// the work is done (later from the partner lab, with a long tail), and each result signed off by the clinician who
    /// asked for it, before the lab finishes its stage (it waits for results). Returns when the stage can be finished,
    /// or null if the story stops first: still at the lab, awaiting sign-off, or the specimen was lost.
    /// </summary>
    private DateTime? RunLab(PatientPathwayStage stage, StaffMember scientist, DateTime startAt, DateTime plannedReportAt)
    {
        var open = _specimens.Where(s => SpecimenWorkflow.IsOpen(s.Status)).ToList();
        if (open.Count == 0)
            return plannedReportAt;

        // Lost on the way to the partner lab: the clinician who asked for it marks it lost when it hasn't arrived, and
        // puts the pathway on hold while a repeat biopsy is arranged.
        if (_plan.LosesSpecimen && open.FirstOrDefault(s => !s.Laboratory.UsesMedApp && s.Status == SpecimenStatus.Sent) is { } missing)
        {
            var lostAt = Working(missing.SentAtUtc!.Value.AddDays(9 + _random.NextDouble() * 4));
            if (lostAt >= _now)
                return null;
            var requester = _world.Staff.Single(s => s.Id == missing.RequestingUserId);
            Lab(missing, requester, lostAt, c => c.MarkLost("Not received by the partner lab: the courier has no record of it after collection."));
            Tell(SpecimenNotifications.Lost(missing, requester.Actor, lostAt), lostAt);
            Change(_lead, lostAt.AddMinutes(_random.Next(5, 40)), c => c.ChangeStatus(Status.Blocked,
                "Specimen lost in transit to the partner lab: repeat biopsy being arranged with the patient."));
            return null;
        }

        var reportAt = plannedReportAt;
        if (open.Any(s => !s.Laboratory.UsesMedApp))
            reportAt = Working(startAt.AddDays((plannedReportAt - startAt).TotalDays * (1.6 + _random.NextDouble())));

        foreach (var specimen in open.Where(s => s.Status == SpecimenStatus.Sent))
        {
            var receiveAt = Working(Max(startAt, specimen.SentAtUtc!.Value.AddMinutes(40)));
            if (receiveAt >= _now)
                return null;
            Lab(specimen, scientist, receiveAt, c => c.Receive(receiveAt, LabReference()));
        }

        if (reportAt >= _now || open.Any(s => s.Status != SpecimenStatus.Received))
            return null;

        var signOffs = new List<DateTime>();
        foreach (var specimen in open)
        {
            var result = Report(specimen, scientist, reportAt);

            // Filed against the wrong specimen: marked entered in error by the lab within the hour (the file deleted,
            // the specimen back to Received), and the right report attached.
            if (_plan.ResultInError && specimen == open[0])
            {
                var wrongAt = reportAt.AddMinutes(_random.Next(15, 50));
                var againAt = wrongAt.AddMinutes(_random.Next(5, 20));
                if (againAt >= _now)
                    return null;
                EnteredInError(result, scientist, wrongAt);
                result = Report(specimen, scientist, againAt);
            }

            var signed = SignOff(result, reportAt);
            if (signed is null)
                return null;
            signOffs.Add(signed.Value);
        }

        return signOffs.Max();
    }

    /// <summary>
    /// Specimens still out when the pathway's story stopped short of its lab stage (put on hold, cancelled, or waiting
    /// for the stage to start): the lab books them in and reports them on its own timeline, as the lab's steps work in
    /// any pathway status, and the requesting clinician signs the result off. Those at a lab stage under way now are
    /// left to it (<see cref="RunLab"/>); a lost one is closed already.
    /// </summary>
    private void FinishOpenSpecimens()
    {
        var current = PathwayWorkflow.CurrentStage(_pathway);
        if (current is { Name: DemoPathwayKinds.Histology or DemoPathwayKinds.LabTests, StartedAtUtc: not null } && PathwayWorkflow.IsOpen(_pathway.Status))
            return;

        var scientist = _world.Member(DemoStaff.ScientistUsername);
        foreach (var specimen in _specimens.Where(s => s.Status is SpecimenStatus.Sent or SpecimenStatus.Received).ToList())
        {
            if (specimen.Status == SpecimenStatus.Sent)
            {
                var receiveAt = Working(specimen.SentAtUtc!.Value.AddHours(2 + _random.NextDouble() * 20));
                if (receiveAt >= _now)
                    continue;
                Lab(specimen, scientist, receiveAt, c => c.Receive(receiveAt, LabReference()));
            }

            var days = specimen.SpecimenType.ExpectedTurnaroundDays * (0.4 + _random.NextDouble() * 0.5)
                       * (specimen.Laboratory.UsesMedApp ? 1 : 1.6 + _random.NextDouble());
            var reportAt = Working(Max(specimen.ReceivedAtUtc!.Value.AddHours(1), specimen.TakenAtUtc.AddDays(days)));
            if (reportAt >= _now)
                continue;
            var result = Report(specimen, scientist, reportAt);
            SignOff(result, reportAt);
        }
    }

    /// <summary>Attaches the lab's report (a synthetic PDF, no values) and makes its result, as ReportSpecimenHandler does.</summary>
    private Result Report(Specimen specimen, StaffMember scientist, DateTime at)
    {
        var histology = specimen.SpecimenType.Name is not ("Blood" or "Urine");
        var bytes = SyntheticFiles.Pdf(histology ? "Histology report (synthetic)" : "Laboratory report (synthetic)",
        [
            "SYNTHETIC: not a real result.",
            $"Specimen: {specimen.Number}, {specimen.SpecimenType.Name}",
            $"Laboratory: {specimen.Laboratory.Name}",
            "Report: as recorded in the stage notes. No values: a demonstration file.",
        ]);
        // On the pathway if it still takes files; otherwise the patient (cancelled or archived meanwhile), as the handler does.
        var onPathway = new AttachmentTarget(specimen.PatientId, _pathway, null).WhyFilesCantChange() is null;
        var report = new Attachment
        {
            Id = Guid.NewGuid(),
            PatientId = specimen.PatientId,
            TeamId = specimen.TeamId,
            PathwayId = onPathway ? _pathway.Id : null,
            FileName = histology ? $"Histology report {specimen.Number}.pdf" : $"{specimen.SpecimenType.Name} report {specimen.Number}.pdf",
            ContentType = "application/pdf",
            SizeBytes = bytes.Length,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            ScanStatus = AttachmentScanStatus.NotScanned,
            UploadedByUserId = scientist.Id,
            UploadedByUsername = scientist.Username,
            UploadedAtUtc = at,
        };
        _attachments.Add(report);
        _files[report.Id] = bytes;

        var change = new SpecimenChange(specimen, _pathway, scientist.Actor, at.AddTicks(1));
        change.Report(report.Id);
        _audit.Add(AttachmentAudit.Entry(report, new AttachmentTarget(specimen.PatientId, onPathway ? _pathway : null, null), scientist.Actor,
            PatientPathwayAuditAction.AttachmentAdded, at));
        _audit.AddRange(change.AuditEntries);

        var result = new Result
        {
            Id = Guid.NewGuid(),
            PatientId = specimen.PatientId,
            TeamId = specimen.TeamId,
            PathwayId = _pathway.Id,
            Pathway = _pathway,
            SpecimenId = specimen.Id,
            Specimen = specimen,
            Source = ResultSource.Specimen,
            Title = ReportSpecimenHandler.ResultTitle(specimen),
            ReportAttachmentId = report.Id,
            ReportedAtUtc = at,
            ReportedByUserId = scientist.Id,
            ResponsibleUserId = specimen.RequestingUserId,
        };
        var resultChange = new ResultChange(result, _pathway, scientist.Actor, at.AddTicks(2));
        resultChange.Record(_world.UsernameOf(specimen.RequestingUserId));
        _audit.AddRange(resultChange.AuditEntries);
        _results.Add(result);
        Touch(scientist, at);
        if (ResultNotifications.Ready(result, scientist.Actor, at) is { } ready)
            Tell([ready], at);
        return result;
    }

    private void EnteredInError(Result result, StaffMember scientist, DateTime at)
    {
        var change = new ResultChange(result, _pathway, scientist.Actor, at);
        change.MarkEnteredInError("Report filed against the wrong specimen; the right report is attached instead.");
        _audit.AddRange(change.AuditEntries);
        var report = _attachments.Single(a => a.Id == result.ReportAttachmentId);
        report.DeletedAtUtc = at;
        report.DeletedByUserId = scientist.Id;
        report.DeletedByUsername = scientist.Username;
        _audit.Add(AttachmentAudit.Entry(report, new AttachmentTarget(result.PatientId, _pathway, null), scientist.Actor,
            PatientPathwayAuditAction.AttachmentDeleted, at.AddTicks(_audit.Count)));
        Touch(scientist, at);
    }

    /// <summary>
    /// The requesting clinician reads the result (sometimes marking it seen first) and signs it off with what they did,
    /// usually within a day or two; for a few, weeks go by. Returns when, or null if not yet.
    /// </summary>
    private DateTime? SignOff(Result result, DateTime reportedAt)
    {
        var clinician = _world.Staff.Single(s => s.Id == result.ResponsibleUserId);
        // Most within a day or so; some take most of a week, a few nearer two (a visiting consultant's, a busy week).
        var roll = _random.NextDouble();
        var delay = _plan.SlowSignOff ? 15 + _random.NextDouble() * 15
            : roll < 0.45 ? 0.05 + _random.NextDouble() * 1.5
            : roll < 0.8 ? 2 + _random.NextDouble() * 5
            : 7 + _random.NextDouble() * 9;
        var signAt = Working(reportedAt.AddDays(delay));

        if (_random.NextDouble() < 0.3)
        {
            var seenAt = Working(reportedAt.AddHours(1 + _random.NextDouble() * 5));
            if (seenAt < signAt && seenAt < _now)
            {
                var seen = new ResultChange(result, _pathway, clinician.Actor, seenAt);
                seen.MarkSeen();
                _audit.AddRange(seen.AuditEntries);
                Touch(clinician, seenAt);
            }
        }

        if (signAt >= _now)
            return null;

        var (action, detail) = ActionFor(result);
        var change = new ResultChange(result, _pathway, clinician.Actor, signAt);
        change.SignOff(action, detail);
        _audit.AddRange(change.AuditEntries);
        Touch(clinician, signAt);
        return signAt;
    }

    /// <summary>
    /// What the clinician recorded doing, from the referral's planned outcome (their choice in the story, never
    /// MedApp's): skin and bleeding results go to the team meeting or the patient is told; blood tests usually need no
    /// further action. Now and then something else, with its detail.
    /// </summary>
    private (ResultAction Action, string? Detail) ActionFor(Result result)
    {
        var roll = _random.NextDouble();
        if (roll < 0.04)
            return (ResultAction.Other, "Discussed with the patient's GP by phone first, as the patient asked.");
        if (roll < 0.08)
            return (ResultAction.ReferredOn, null);
        if (IsSkin && _plan.PathwayKind == PathwayKind.SkinLesion)
            return (roll < 0.75 ? ResultAction.ForMdtDiscussion : ResultAction.AppointmentArranged, null);
        if (_plan.PathwayKind == PathwayKind.PostMenopausalBleeding || IsSkin)
            return (roll < 0.6 ? ResultAction.PatientInformed : ResultAction.AppointmentArranged, null);
        return (roll < 0.7 ? ResultAction.NoFurtherAction : ResultAction.PatientInformed, null);
    }

    // The MDT -------------------------------------------------------------------------------------------------

    /// <summary>
    /// A skin lesion's results and team review: the lead lists it for the next skin cancer MDT with their question, and
    /// the chair records the outcome the team agreed (the referral's planned outcome, in the team's words) and who was
    /// there, as ListForMdtHandler and RecordMdtOutcomeHandler do. Returns when the outcome was recorded, or null if the
    /// meeting hasn't happened yet (the item is still listed).
    /// </summary>
    private DateTime? Mdt(PatientPathwayStage stage, DateTime startAt)
    {
        var listAt = startAt.AddMinutes(_random.Next(5, 90));
        var meeting = _world.MdtMeetings.FirstOrDefault(m => m.StartUtc > listAt.AddHours(4) && m.StartUtc < listAt.AddDays(15));
        if (meeting is null || listAt >= _now)
            return listAt < _now ? listAt : null;

        MdtRules.EnsurePathwayCanBeListed(_pathway);
        MdtRules.EnsureCanListFor(meeting, listAt);
        var team = _world.Teams[meeting.TeamId!.Value];
        var item = new MdtItem
        {
            Id = Guid.NewGuid(),
            PatientId = _pathway.PatientId,
            PathwayId = _pathway.Id,
            Pathway = _pathway,
            TeamId = team.Id,
            Team = team,
            MeetingId = meeting.Id,
            Meeting = meeting,
            Question = MdtQuestion(),
            Status = MdtItemStatus.Listed,
            ListedByUserId = _lead.Id,
            ListedByUsername = _lead.Username,
            ListedAtUtc = listAt,
            Version = 1,
        };
        _audit.Add(MdtAudit.Listed(item, _pathway, team.Name, meeting, _lead.Actor, listAt));
        _mdtItems.Add(item);
        Touch(_lead, listAt);

        var outcomeAt = meeting.StartUtc.AddMinutes(_random.Next(5, 55));
        if (outcomeAt >= _now)
            return null;

        var chair = _world.Lead;
        MdtRules.EnsureCanRecordOutcome(item, outcomeAt);
        // Only the meeting's attendees can have been present: those who hadn't declined.
        var present = meeting.Attendees.Where(a => a.Reply != MeetingReply.Declined).Select(a => a.StaffUserId).Distinct().ToList();
        if (present.Count == 0)
            present = meeting.Attendees.Select(a => a.StaffUserId).ToList();
        item.Status = MdtItemStatus.Discussed;
        item.Outcome = $"Histology reviewed by the team. Agreed: {Lower(_plan.NoteContext().Result.Plan.TrimEnd('.'))}.";
        item.OutcomeByUserId = chair.Id;
        item.OutcomeByUsername = chair.Username;
        item.OutcomeAtUtc = outcomeAt;
        item.Attendees = present.Select(id => new MdtItemAttendee { MdtItemId = item.Id, StaffUserId = id, Username = _world.UsernameOf(id) }).ToList();
        item.Version++;
        _audit.Add(MdtAudit.OutcomeRecorded(item, team.Name, chair.Actor, outcomeAt));
        Touch(chair, outcomeAt);
        Tell(MdtNotifications.OutcomeRecorded(item, chair.Actor, outcomeAt), outcomeAt);
        return outcomeAt;
    }

    private string MdtQuestion() => _random.Next(3) switch
    {
        0 => $"Histology back for the lesion on the {_plan.Side ?? "skin"}: please agree the plan.",
        1 => "Is further treatment needed, and if so what? Report attached to the pathway.",
        _ => $"Margins and next steps for the {_plan.Kind.Short} on the {_plan.Side ?? "skin"}.",
    };

    // Letters -------------------------------------------------------------------------------------------------

    /// <summary>
    /// The discharge summary a discharge letter stage needs (decision 105): written by the lead from the record, approved
    /// by them (or, for a nurse who can't approve letters, submitted to the clinical lead), and recorded as posted by the
    /// booking coordinator, before the stage is finished. Planned here; written through the letter commands once the
    /// pathways are saved. Returns when the stage can be finished, or null if the letter isn't sent yet.
    /// </summary>
    private DateTime? PlanLetter(PatientPathwayStage stage, StaffMember author, DateTime startAt)
    {
        var writtenAt = startAt.AddMinutes(_random.Next(5, 90));
        if (writtenAt >= _now)
            return null;

        var context = _plan.NoteContext();
        var body = DischargeBody(context);
        var canApprove = author.PermissionsAt(writtenAt).Contains(Permission.ApproveLetters);
        var approver = canApprove ? null : _world.Lead;
        DateTime? submittedAt = canApprove ? null : writtenAt.AddMinutes(_random.Next(20, 60));
        var returned = !canApprove && _plan.LetterReturned;
        DateTime? returnedAt = returned ? Working(submittedAt!.Value.AddHours(2 + _random.NextDouble() * 20)) : null;
        var approvedAt = Working((returnedAt ?? writtenAt).AddDays(0.1 + _random.NextDouble() * 1.2));
        var sentAt = Working(approvedAt.AddDays(0.02 + _random.NextDouble() * 0.8));
        var coordinator = _world.Coordinator;

        _letter = new LetterPlan(_pathway.Id, _pathway.PatientId, _world.Teams[_pathway.TeamId].Name,
            LetterKind.DischargeSummary, author, writtenAt, body, approver,
            submittedAt < _now ? submittedAt : null,
            approvedAt < _now ? approvedAt : null,
            coordinator,
            sentAt < _now ? sentAt : null)
        {
            ReturnedUtc = returnedAt < _now ? returnedAt : null,
        };
        Touch(author, writtenAt);
        return sentAt < _now ? sentAt.AddMinutes(_random.Next(5, 60)) : null;
    }

    private static string DischargeBody(NoteContext c) =>
        "Re: {{patient.name}}, born {{patient.dateOfBirth}}\n{{patient.address}}\n\n" +
        "Dear colleague at {{practice.name}},\n\nYour patient has been discharged from our care.\n\n" +
        $"What we found:\n{c.Result.Seen}\n{(c.Result.Result.Length > 0 ? c.Result.Result + "\n" : string.Empty)}\n" +
        $"What we did:\n{(c.Result.Treatment.Length > 0 ? c.Result.Treatment : "Assessment and advice in clinic.")}\n\n" +
        $"What we ask of you:\n{c.Result.Plan}.\n\nThis is a synthetic letter for a demonstration.";

    // Skipping and missed visits ------------------------------------------------------------------------------

    /// <summary>
    /// An optional stage (the last biomechanics session) skipped by the lead when the patient declines it, as
    /// SkipPathwayStageHandler does (a Required visit's skip says why); for one, reinstated days later when they change
    /// their mind, as ReinstatePathwayStageHandler does, and then worked as usual. Returns how the stage ended, or null
    /// to carry on with it (from <paramref name="startAt"/>, moved on after a reinstatement).
    /// </summary>
    private StageOutcome? MaybeSkip(PatientPathwayStage stage, ref DateTime reached, ref DateTime startAt)
    {
        if (!stage.IsOptional || !LabAndLetters(startAt) || (!_plan.ReinstatesSkipped && _random.NextDouble() >= 0.2))
            return null;
        if (PathwayWorkflow.WhySkipNotAllowed(_pathway, stage, _lead.PermissionsAt(startAt).Contains(Permission.FinishWithoutVisit)) is not null)
            return null;

        var next = StageFinishing.NextStage(_pathway, stage);
        var appointments = _appointments.Where(a => a.StageId == stage.Id || (next is not null && a.StageId == next.Id)).ToList();
        var skipAt = startAt;
        Change(_lead, skipAt, change => StageFinishing.Skip(change, stage, StageSkipReason.PatientDeclined,
            "The patient feels well enough to carry on with the home programme alone.", appointments));
        if (!_plan.ReinstatesSkipped)
        {
            reached = skipAt;
            return StageOutcome.Finished;
        }

        var reinstateAt = Working(skipAt.AddDays(3 + _random.NextDouble() * 5));
        if (reinstateAt >= _now)
            return StageOutcome.Stopped;
        Change(_lead, reinstateAt, change =>
        {
            change.ReinstateStage(stage, "The patient rang: they'd like the last session after all.");
            if (PathwayWorkflow.CurrentStage(_pathway)?.Id == stage.Id && stage.VisitRequirement != VisitRequirement.None)
                StageVisits.RequestForCurrentStage(change, _appointments.Where(a => a.StageId == stage.Id).ToList());
        });
        startAt = Working(reinstateAt.AddMinutes(_random.Next(5, 90)));
        return startAt >= _now ? StageOutcome.Stopped : null;
    }

    /// <summary>
    /// What the booking coordinator decided after a Did not attend, once that was recorded in MedApp (from when the lab,
    /// results and letters went live): usually another appointment, sometimes a clinician to review it first. A few
    /// from the last week are still waiting for a decision. Returns when it was decided (or the visit, before then), or
    /// null if it's still waiting.
    /// </summary>
    private DateTime? DnaFollowUp(Appointment appointment, PatientPathwayStage stage, DateTime endedAt)
    {
        if (!LabAndLetters(endedAt))
            return endedAt;

        var decideAt = Working(endedAt.AddHours(2 + _random.NextDouble() * 20));
        if (decideAt >= _now || (endedAt > _now.AddDays(-7) && _random.NextDouble() < 0.5))
            return null;

        var coordinator = _world.Coordinator;
        var decision = _random.NextDouble() < 0.8 ? DnaFollowUpDecision.Rebook : DnaFollowUpDecision.ClinicianToReview;
        var change = new AppointmentChange(appointment, coordinator.Actor, decideAt, StaffName(appointment), _pathway.Title, stage.Name);
        change.RecordDnaFollowUp(decision, decision == DnaFollowUpDecision.ClinicianToReview ? "Second missed visit: asked the clinician whether to offer another." : null);
        _events.AddRange(change.Events);
        Touch(coordinator, decideAt);
        return decideAt;
    }

    // Shared ---------------------------------------------------------------------------------------------------

    /// <summary>Notifications staff would have had, in the window the demo keeps them (the last 30 days).</summary>
    private void Tell(IEnumerable<Domain.Notifications.Notification> notifications, DateTime at)
    {
        if (at >= _world.Timeline.NotificationsFromUtc)
            _notifications.AddRange(notifications.Where(n => _world.Delivers(n, at)));
    }

    private void CommitLab()
    {
        _world.Specimens.AddRange(_specimens);
        _world.Results.AddRange(_results);
        _world.ConsentRecords.AddRange(_consents);
        _world.MdtItems.AddRange(_mdtItems);
        _world.Attachments.AddRange(_attachments);
        foreach (var (id, bytes) in _files)
            _world.FileBytes[id] = bytes;
        if (_letter is not null)
            _world.LetterPlans.Add(_letter);
    }

    private static string Capital(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static string Lower(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];
}

/// <summary>Why the lab rejected the one specimen it rejects.</summary>
internal static class SpecimenRejection
{
    public const SpecimenRejectionReason Reason = SpecimenRejectionReason.Unlabelled;
    public const string Detail = "Pot arrived without a label; the request form was with it.";
}
