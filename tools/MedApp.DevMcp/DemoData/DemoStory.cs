using MedApp.Application.Attachments.Commands.UploadAttachment;
using MedApp.Application.Consent.Commands.WithdrawConsent;
using MedApp.Application.Consent.Setup;
using MedApp.Application.Consults.Commands.RequestConsult;
using MedApp.Application.Letters.Commands.ApproveLetter;
using MedApp.Application.Letters.Commands.SendLetter;
using MedApp.Application.Letters.Commands.SubmitLetter;
using MedApp.Application.Letters.Common;
using MedApp.Application.Pathways.PatientPathways.Commands.AddPathwayBranch;
using MedApp.Application.Pathways.PatientPathways.Commands.ChangePatientPathwayStatus;
using MedApp.Application.Referrals.Commands.AcceptReferral;
using MedApp.Application.Referrals.Commands.RegisterReferral;
using MedApp.Application.Results.Commands.MarkResultSeen;
using MedApp.Application.Results.Commands.RecordExternalResult;
using MedApp.Application.Results.Commands.SignOffResult;
using MedApp.Application.Specimens.Commands.RejectSpecimen;
using MedApp.Contracts.Letters.Responses;
using MedApp.Domain.Letters;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using static MedApp.DevMcp.DemoData.DemoPatients;
using static MedApp.DevMcp.DemoData.DemoReplay;
using static MedApp.DevMcp.DemoData.DemoSetup;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// The clinic's patients and their pathways at the same points as the light demo's (<c>MedApp.Web/src/demo/seed.ts</c>
/// and its slices), each replayed through the app's handlers by the people who did the work, at the moments it was done:
/// referrals, visits, notes, consent, samples and their lab cases, results, letters and a consult. Where a stage's gate
/// needs something the light demo leaves out (a visit, a consent record), the story adds it, so every stage was finished
/// as the app would let it be.
/// </summary>
public static class DemoStory
{
    public const string SuspectedSkinCancer = "Suspected skin cancer";
    public const string ChangingMole = "Changing mole or lesion check";
    public const string InflammatorySkin = "Inflammatory skin assessment";
    public const string SkinInfection = "Suspected skin infection";

    private const string Triage = "Triage";
    private const string Dermoscopy = "Dermoscopy assessment";
    private const string TakeBiopsy = "Take biopsy";
    private const string Histology = "Histology";
    private const string HistologyResult = "Histology result";
    private const string UncertainBiopsy = "Uncertain: biopsy";

    /// <summary>Who referred (synthetic): a practice and a clinician by name, as typed from the letter.</summary>
    private const string ReferrerPractice = "Example Lane Surgery (synthetic)";
    private const string ReferrerClinician = "Dr A. Sample";

    /// <summary>The day before a visit, at noon: when the booking coordinator booked it.</summary>
    private static DateTime BookedFor(DateTime visit) => visit.AddDays(-1).Date.AddHours(12);

    /// <summary>The next weekday after a date.</summary>
    private static DateOnly NextWeekday(DateOnly date)
    {
        do date = date.AddDays(1);
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        return date;
    }

    public static async Task RunAsync(DemoSeed seed)
    {
        await ReferralsAsync(seed);
        await EllisVardenAsync(seed);
        await PriyaHollowayAsync(seed);
        await SamuelOkaforAsync(seed);
        await TobiasLundAsync(seed);
        await IlonaPetrovaBloodsAsync(seed);
        await HarrietQuillAsync(seed);
        await FelixMoretonAsync(seed);
        await AishaKhouryAsync(seed);
        await CallumAshbyAsync(seed);
        await NoraFennickAsync(seed);
        await LeahCorrinAsync(seed);
        await IlonaPetrovaScrapingAsync(seed);
    }

    /// <summary>The referrals awaiting triage: Oliver Maddox's urgent one, and Aisha Khoury's (she has a pathway already).</summary>
    private static async Task ReferralsAsync(DemoSeed seed)
    {
        var t = seed.Timeline;
        var dermatology = seed.TeamId(DemoPeople.Dermatology);
        var oliver = await seed.Send(t.Ago(2, 11, 0), DemoPeople.Marsh, new RegisterReferralCommand(seed.Patients[OliverMaddox], dermatology, t.DaysFrom(-3),
            ReferrerPractice, ReferrerClinician, null, "Urgent", "Rapidly growing lesion on the scalp, bleeds on contact.", false, DemoPeople.Marsh.Actor), "register Oliver Maddox's referral");
        seed.Tour["referralToTriage"] = oliver.Id;
        await seed.Send(t.Ago(5, 11, 0), DemoPeople.Marsh, new RegisterReferralCommand(seed.Patients[AishaKhoury], dermatology, t.DaysFrom(-6),
            ReferrerPractice, ReferrerClinician, null, "Medium", "Persistent rash on both hands despite emollients.", true, DemoPeople.Marsh.Actor), "register Aisha Khoury's referral");
    }

    /// <summary>
    /// Ellis Varden (biopsy to book): referred, accepted by Nadia Thornbury, seen in clinic (dermoscopy uncertain), so
    /// his Take biopsy is next, its visit to book. His consent to the biopsy, recorded on the form's older version, was
    /// withdrawn; his clinic letter is her draft; he's on today's MDT to discuss.
    /// </summary>
    private static async Task EllisVardenAsync(DemoSeed seed)
    {
        var t = seed.Timeline;
        var nadia = DemoPeople.Thornbury;
        var referral = await seed.Send(t.Ago(15, 11, 0), DemoPeople.Marsh, new RegisterReferralCommand(seed.Patients[EllisVarden], seed.TeamId(DemoPeople.Dermatology),
            t.DaysFrom(-16), ReferrerPractice, ReferrerClinician, null, "High", "Scaly lesion on the left forearm, not healing for 4 months.", false,
            DemoPeople.Marsh.Actor), "register Ellis Varden's referral");
        var accepted = await seed.Send(t.Ago(14, 10, 0), nadia, new AcceptReferralCommand(referral.Id, seed.Templates[SuspectedSkinCancer], "High",
            "Scaly lesion, left forearm", null, null, referral.Version, nadia.Actor), "accept Ellis Varden's referral");
        var ellis = new StoryPathway(accepted.Id, seed.Patients[EllisVarden], nadia, accepted);
        seed.Tour["biopsyToBook"] = ellis.Id;
        await AssignAsync(seed, ellis, t.Ago(14, 10, 5));
        await DoAsync(seed, ellis, Triage, t.Ago(14, 10, 10));

        var visit = DemoTimeline.At(t.WeekdayBack(10), 9, 30);
        await VisitAsync(seed, ellis, Dermoscopy, nadia, visit, 30, ClinicRoom1, BookedFor(visit).AddDays(-1));
        await StartAsync(seed, ellis, Dermoscopy, visit);
        await FinishAsync(seed, ellis, Dermoscopy, visit.AddMinutes(35), UncertainBiopsy);

        // His consent to the biopsy, on the form's older version, then withdrawn; the older version is archived after.
        var consent = await ConsentAsync(seed, ellis, TakeBiopsy, AdultFormOld, visit.AddMinutes(40));
        await NoteAsync(seed, ellis, nadia, visit.AddMinutes(95), "Seen in clinic: a non-healing scaly lesion, not pigmented. Plan a punch biopsy within two weeks.");
        await seed.Send(visit.AddDays(1), DemoPeople.Admin, new SetConsentFormArchivedCommand(seed.ConsentForms[AdultFormOld], true), "archive the older consent form");
        var version = await seed.Db(db => db.ConsentRecords.Where(c => c.Id == consent).Select(c => c.Version).SingleAsync(seed.Ct));
        await seed.Send(t.Ago(4, 14, 0), nadia, new WithdrawConsentCommand(consent, t.DaysFrom(-4), "The patient would like to talk it over at home first.", version, nadia.Actor),
            "withdraw Ellis Varden's consent");

        await WriteLetterAsync(seed, ellis, nadia, ClinicLetter, t.Ago(9, 14, 0),
            ClinicBody("Non-healing scaly lesion, left forearm.", "A raised, scaly, non-pigmented lesion of about 10 mm, present for 4 months.", string.Empty),
            t.Ago(9, 14, 20));
        seed.Stories[EllisVarden] = ellis;
    }

    /// <summary>
    /// Priya Holloway (histology to finish): biopsied with her consent; the lab received her sample, picked up her
    /// Histology and worked its case up to the report (MC16). Tom Hallworth's clinic letter awaits Nadia Thornbury's approval.
    /// </summary>
    private static async Task PriyaHollowayAsync(DemoSeed seed)
    {
        var t = seed.Timeline;
        var nadia = DemoPeople.Thornbury;
        var priya = await CreateAsync(seed, PriyaHolloway, SuspectedSkinCancer, "Non-pigmented lesion, upper back", 20, "High", t.Ago(28, 9, 15), nadia);
        seed.Tour["histologyToFinish"] = priya.Id;
        await DoAsync(seed, priya, Triage, t.Ago(28, 9, 30));

        var first = DemoTimeline.At(t.WeekdayBack(20), 9, 0);
        await VisitAsync(seed, priya, Dermoscopy, nadia, first, 30, ClinicRoom1, t.Ago(27, 12, 0));
        await StartAsync(seed, priya, Dermoscopy, first);
        await FinishAsync(seed, priya, Dermoscopy, first.AddMinutes(35), UncertainBiopsy);

        var biopsy = DemoTimeline.At(t.WeekdayBack(12), 11, 0);
        await VisitAsync(seed, priya, TakeBiopsy, nadia, biopsy, 30, ClinicRoom1, t.Ago(18, 12, 0), attended: false);
        await ConsentAsync(seed, priya, TakeBiopsy, AdultForm, biopsy.AddMinutes(-10));
        await StartAsync(seed, priya, TakeBiopsy, biopsy);
        var sample = await SampleAsync(seed, priya, TakeBiopsy, SkinBiopsy, SkinHistology, biopsy.AddMinutes(20), "Upper back, left of midline",
            "Non-pigmented lesion, 7 mm. Query basal cell carcinoma.");
        await AttendedAsync(seed, priya, TakeBiopsy, biopsy.AddMinutes(31));
        await FinishAsync(seed, priya, TakeBiopsy, biopsy.AddMinutes(35));
        seed.Tour["priyaSpecimen"] = sample.Id;

        await SendSampleAsync(seed, sample.Id, biopsy.AddMinutes(260), nadia, "BATCH-0412");
        var received = biopsy.AddMinutes(20).AddDays(1);
        await ReceiveSampleAsync(seed, sample.Id, received, "EVP-55120");
        await StartAsync(seed, priya, Histology, received.AddMinutes(5), DemoPeople.Aldous);
        var labCase = await CaseOfAsync(seed, sample.Id);
        seed.Tour["labCaseStageToReport"] = labCase;
        await WorkCaseAsync(seed, labCase, received.AddMinutes(5), t.HoursAgo(4), [.. DemoReplay.Histology, LabWork.NoMoreWork]);

        // Tom Hallworth's clinic letter, sent to Nadia Thornbury to approve.
        var tom = DemoPeople.Hallworth;
        var letter = await WriteLetterAsync(seed, priya, tom, ClinicLetter, t.Ago(2, 11, 0),
            ClinicBody("Non-pigmented lesion, upper back.", "A biopsy was taken; histology is awaited.", "We will write again with the result and the plan."), t.Ago(2, 11, 20));
        letter = await seed.Send(t.Past(t.Ago(1, 15, 0)), tom, new SubmitLetterCommand(letter.Id, nadia.Id, letter.Version, tom.Actor), "submit a letter");
        seed.Tour["letterToApprove"] = letter.Id;
        seed.Stories[PriyaHolloway] = priya;
    }

    /// <summary>
    /// Samuel Okafor (on hold): Tom Hallworth's pathway; his scraping went to the lab and is overdue there, his
    /// Microbiology the lab's since. On hold while he's away; his clinic letter approved and still to send.
    /// </summary>
    private static async Task SamuelOkaforAsync(DemoSeed seed)
    {
        var t = seed.Timeline;
        var tom = DemoPeople.Hallworth;
        var samuel = await CreateAsync(seed, SamuelOkafor, SkinInfection, "Scaly rash on both hands", 40, "Medium", t.Ago(40, 9, 15), tom);
        seed.Tour["onHold"] = samuel.Id;
        await DoAsync(seed, samuel, Triage, t.Ago(40, 9, 30));

        // Tom Hallworth works Tuesday to Thursday.
        var assessment = DemoTimeline.At(DemoTimeline.OnOrBefore(t.DaysFrom(-30), DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday), 10, 0);
        await VisitAsync(seed, samuel, "Infection assessment", tom, assessment, 30, ClinicRoom1, t.Ago(38, 12, 0));
        await StartAsync(seed, samuel, "Infection assessment", assessment);
        await FinishAsync(seed, samuel, "Infection assessment", assessment.AddMinutes(35), "Sample needed");

        var sampleVisit = DemoTimeline.At(DemoTimeline.OnOrBefore(t.DaysFrom(-13), DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday), 10, 15);
        await VisitAsync(seed, samuel, "Take skin sample", tom, sampleVisit, 30, ClinicRoom1, assessment.AddDays(2).Date.AddHours(12), attended: false);
        await StartAsync(seed, samuel, "Take skin sample", sampleVisit);
        var sample = await SampleAsync(seed, samuel, "Take skin sample", SkinScraping, FungalScraping, sampleVisit.AddMinutes(15), "Both hands", null);
        await AttendedAsync(seed, samuel, "Take skin sample", sampleVisit.AddMinutes(31));
        await FinishAsync(seed, samuel, "Take skin sample", sampleVisit.AddMinutes(32));
        await SendSampleAsync(seed, sample.Id, sampleVisit.Date.AddHours(15), tom, "BATCH-0398");
        seed.Tour["specimenOverdue"] = sample.Id;

        // His clinic letter, by Tom Hallworth, approved by Nadia Thornbury: still to send to both recipients.
        var letter = await WriteLetterAsync(seed, samuel, tom, ClinicLetter, t.Ago(3, 10, 0),
            ClinicBody("Scaly rash on both hands, under investigation.", "A skin scraping was sent to the lab.", "We will review him with the result when he is back."),
            t.Ago(3, 10, 15));
        letter = await seed.Send(t.Ago(3, 10, 30), tom, new SubmitLetterCommand(letter.Id, DemoPeople.Thornbury.Id, letter.Version, tom.Actor), "submit a letter");
        await NoteAsync(seed, samuel, tom, t.Ago(3, 11, 5), "On hold while the patient is away for a month.");
        samuel.Latest = await seed.Send(t.Ago(3, 14, 10), tom, new ChangePatientPathwayStatusCommand(samuel.Id, nameof(PatientPathwayStatus.Blocked),
            "The patient is away for a month.", tom.Actor), "put Samuel Okafor's pathway on hold");
        letter = await seed.Send(t.Past(t.Ago(1, 10, 30)), DemoPeople.Thornbury, new ApproveLetterCommand(letter.Id, letter.Version, DemoPeople.Thornbury.Actor), "approve a letter");
        seed.Tour["letterToSend"] = letter.Id;
        seed.Stories[SamuelOkafor] = samuel;
    }

    /// <summary>Tobias Lund (unassigned): a new pathway at Triage, with a report from his GP practice for Tom Hallworth to sign off.</summary>
    private static async Task TobiasLundAsync(DemoSeed seed)
    {
        var t = seed.Timeline;
        var tobias = await CreateAsync(seed, TobiasLund, ChangingMole, "New lesion, right cheek", 60, "Medium", t.Ago(2, 9, 15), DemoPeople.Thornbury, assign: false);
        seed.Tour["unassigned"] = tobias.Id;

        // The GP practice's report, filed on his pathway by Bea Sandiford for Tom Hallworth to sign off (an external result).
        var bea = DemoPeople.Sandiford;
        var bytes = SyntheticFiles.Pdf("Report from the GP practice (synthetic)",
            ["A synthetic report for the demo.", "No real patient or clinical details."]);
        using var content = new MemoryStream(bytes);
        var file = await seed.Send(t.Past(t.Ago(1, 11, 25)), bea, new UploadAttachmentCommand(null, tobias.Id, null, null, "gp-report.pdf", bytes.Length,
            content, bea.Actor, false), "file the GP's report");
        var result = await seed.Send(t.Past(t.Ago(1, 11, 30)), bea, new RecordExternalResultCommand(file.Id, "Report from the GP practice", tobias.Id,
            DemoPeople.Hallworth.Id, bea.Actor), "record the GP's report as a result");
        seed.Tour["gpReport"] = result.Id;
        seed.Stories[TobiasLund] = tobias;
    }

    /// <summary>
    /// Ilona Petrova's itchy rash: her first blood sample rejected by the lab (insufficient), a repeat taken the next
    /// day and received, its case worked up to the report; her follow-up for the results booked; her clinic letter
    /// approved by Tom Hallworth and sent to everyone.
    /// </summary>
    private static async Task IlonaPetrovaBloodsAsync(DemoSeed seed)
    {
        var t = seed.Timeline;
        var nadia = DemoPeople.Thornbury;
        var ilona = await CreateAsync(seed, IlonaPetrova, InflammatorySkin, "Itchy rash, arms and legs", 25, "Medium", t.Ago(45, 9, 15), nadia);
        seed.Tour["followUpBooked"] = ilona.Id;
        await DoAsync(seed, ilona, Triage, t.Ago(45, 9, 30));

        var assessment = DemoTimeline.At(t.WeekdayBack(30), 10, 0);
        await VisitAsync(seed, ilona, "Skin assessment", nadia, assessment, 30, ClinicRoom1, t.Ago(43, 12, 0));
        await StartAsync(seed, ilona, "Skin assessment", assessment);
        await FinishAsync(seed, ilona, "Skin assessment", assessment.AddMinutes(35), "Blood tests needed");

        // Two blood tests on consecutive weekdays: the first sample too small, the second received.
        var secondDay = t.WeekdayBack(3);
        var firstDay = t.WeekdayBack(4);
        if (firstDay >= secondDay)
            firstDay = DemoTimeline.OnOrBefore(secondDay.AddDays(-1), DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday);
        var firstVisit = DemoTimeline.At(firstDay, 9, 45);
        await VisitAsync(seed, ilona, "Take bloods", nadia, firstVisit, 15, ClinicRoom1, firstVisit.AddDays(-3).Date.AddHours(12), attended: false);
        await StartAsync(seed, ilona, "Take bloods", firstVisit);
        var first = await SampleAsync(seed, ilona, "Take bloods", BloodSample, FullBloodCount, firstVisit.AddMinutes(10), null, null);
        await AttendedAsync(seed, ilona, "Take bloods", firstVisit.AddMinutes(16));
        await SendSampleAsync(seed, first.Id, firstVisit.AddMinutes(135), nadia);
        var rejectedAt = DemoTimeline.At(secondDay, 9, 0);
        var version = await SpecimenVersionAsync(seed, first.Id);
        await seed.Send(rejectedAt, DemoPeople.Aldous, new RejectSpecimenCommand(first.Id, "InsufficientSample", "Too little blood for the tests asked for.", version,
            DemoPeople.Aldous.Actor), "reject a sample");

        var secondVisit = DemoTimeline.At(secondDay, 10, 45);
        await VisitAsync(seed, ilona, "Take bloods", nadia, secondVisit, 15, ClinicRoom1, rejectedAt.AddMinutes(10));
        var second = await SampleAsync(seed, ilona, "Take bloods", BloodSample, FullBloodCount, secondVisit.AddMinutes(15), null,
            "Repeat sample: the first was insufficient.");
        await SendSampleAsync(seed, second.Id, secondVisit.AddMinutes(135), nadia);
        await FinishAsync(seed, ilona, "Take bloods", secondVisit.AddMinutes(140));
        var received = t.WeekdayBack(2) > secondDay ? DemoTimeline.At(t.WeekdayBack(2), 10, 0) : secondVisit.AddHours(5);
        await ReceiveSampleAsync(seed, second.Id, received, "CL-20931");
        await StartAsync(seed, ilona, "Blood tests", received.AddMinutes(5), DemoPeople.Aldous);
        seed.Tour["specimenAtLab"] = second.Id;
        var labCase = await CaseOfAsync(seed, second.Id);
        seed.Tour["labCaseToReport"] = labCase;
        await WorkCaseAsync(seed, labCase, received.AddMinutes(5), t.HoursAgo(2),
            LabWork.Accession, LabWork.Adequate, LabWork.TestStep("Analyser run"), LabWork.NoMoreWork);

        // Her follow-up to go through the results, booked for Blood results (still to come).
        var followUpDay = t.OpenDayAhead(1);
        var followUp = DemoTimeline.At(followUpDay, followUpDay.DayOfWeek is DayOfWeek.Tuesday or DayOfWeek.Thursday ? 14 : 11, 0);
        await VisitAsync(seed, ilona, "Blood results", nadia, followUp, 20, ClinicRoom1, t.Past(t.Ago(1, 12, 0)), attended: false);

        // Her clinic letter: Nadia Thornbury's, approved by Tom Hallworth, sent to the GP and to her.
        var tom = DemoPeople.Hallworth;
        var letter = await WriteLetterAsync(seed, ilona, nadia, ClinicLetter, secondVisit.AddMinutes(50),
            ClinicBody("Itchy rash on the arms and legs; cause not yet known.",
                "Scattered itchy bumps on the arms and legs, with scratch marks. Nothing typical of eczema or psoriasis.",
                "Blood tests taken. We will go through the results with her at the booked appointment."), secondVisit.AddMinutes(60));
        letter = await seed.Send(secondVisit.AddMinutes(65), nadia, new SubmitLetterCommand(letter.Id, tom.Id, letter.Version, nadia.Actor), "submit a letter");
        letter = await seed.Send(secondVisit.AddMinutes(75), tom, new ApproveLetterCommand(letter.Id, letter.Version, tom.Actor), "approve a letter");
        await PostAsync(seed, letter, secondVisit.Date.AddHours(16));
        seed.Stories["Ilona Petrova (bloods)"] = ilona;
    }

    /// <summary>
    /// Harriet Quill (referred to hospital): biopsied; the lab reported a basal cell carcinoma, which Nadia Thornbury
    /// signed off for the MDT; at her results visit the outcome was recorded (low-risk BCC), so Refer to hospital is
    /// current, waiting for the onward referral letter, Nadia Thornbury's draft. Her target date has passed.
    /// </summary>
    private static async Task HarrietQuillAsync(DemoSeed seed)
    {
        var t = seed.Timeline;
        var nadia = DemoPeople.Thornbury;
        var harriet = await CreateAsync(seed, HarrietQuill, SuspectedSkinCancer, "Basal cell carcinoma, left temple", -3, "High", t.Ago(40, 9, 15), nadia);
        seed.Tour["referredToHospital"] = harriet.Id;
        await DoAsync(seed, harriet, Triage, t.Ago(40, 9, 30));

        var first = DemoTimeline.At(t.WeekdayBack(36), 10, 0);
        await VisitAsync(seed, harriet, Dermoscopy, nadia, first, 30, ClinicRoom1, t.Ago(39, 12, 0));
        await StartAsync(seed, harriet, Dermoscopy, first);
        await FinishAsync(seed, harriet, Dermoscopy, first.AddMinutes(35), UncertainBiopsy);

        var biopsy = DemoTimeline.At(t.WeekdayBack(28), 11, 30);
        await VisitAsync(seed, harriet, TakeBiopsy, nadia, biopsy, 30, ProcedureRoom, first.AddDays(2).Date.AddHours(12), attended: false);
        await ConsentAsync(seed, harriet, TakeBiopsy, AdultForm, biopsy.AddMinutes(-10));
        await StartAsync(seed, harriet, TakeBiopsy, biopsy);
        var sample = await SampleAsync(seed, harriet, TakeBiopsy, SkinBiopsy, SkinHistology, biopsy.AddMinutes(20), "Left temple", null);
        await AttendedAsync(seed, harriet, TakeBiopsy, biopsy.AddMinutes(31));
        await FinishAsync(seed, harriet, TakeBiopsy, biopsy.AddMinutes(35));
        await SendSampleAsync(seed, sample.Id, biopsy.AddMinutes(260), nadia);
        var received = biopsy.AddMinutes(20).AddDays(1);
        await ReceiveSampleAsync(seed, sample.Id, received, "EVP-54871");
        await StartAsync(seed, harriet, Histology, received.AddMinutes(5), DemoPeople.Aldous);

        // Reported two weeks ago: the lab finishes its Histology, which hands the pathway back to Dermatology.
        var reportedAt = t.Ago(14, 14, 0);
        var labCase = await CaseOfAsync(seed, sample.Id);
        await WorkCaseAsync(seed, labCase, received.AddMinutes(5), reportedAt.AddMinutes(-30), [.. DemoReplay.Histology, LabWork.NoMoreWork]);
        await AuthoriseAsync(seed, labCase, reportedAt, "Basal cell carcinoma, nodular type.");
        await FinishAsync(seed, harriet, Histology, reportedAt.AddMinutes(5), by: DemoPeople.Aldous);

        // Seen, then signed off for the MDT by Nadia Thornbury.
        var resultId = await seed.Db(db => db.Results.Where(r => r.SpecimenId == sample.Id).Select(r => r.Id).SingleAsync(seed.Ct));
        var resultVersion = await seed.Db(db => db.Results.Where(r => r.Id == resultId).Select(r => r.Version).SingleAsync(seed.Ct));
        var seen = await seed.Send(t.Ago(6, 9, 0), nadia, new MarkResultSeenCommand(resultId, resultVersion, nadia.Actor), "mark a result seen");
        await seed.Send(t.Ago(5, 16, 0), nadia, new SignOffResultCommand(resultId, "ForMdtDiscussion", null, seen.Version, nadia.Actor), "sign off a result");
        await NoteAsync(seed, harriet, nadia, t.Ago(6, 11, 5), "Team review: photograph, then refer to the hospital for excision.");

        // Her onward referral to the hospital (MC28): Nadia Thornbury's draft, the hospital typed in, the GP copied.
        var gp = await seed.Db(db => db.Practices.Where(p => p.Id == seed.Practices[ExampleLane]).Select(p => new { p.Id, p.Name }).SingleAsync(seed.Ct));
        await WriteLetterAsync(seed, harriet, nadia, OnwardReferral, t.Past(t.Ago(1, 15, 0)),
            $"{OnwardReferralOpening}\n\n## Reason for referral\n\nExcision of a basal cell carcinoma, left temple.\n\n## Findings and results\n\nA nodular lesion of about 6 mm. The biopsy was reported as a nodular basal cell carcinoma (synthetic report).\n\n## What we are asking for\n\nSurgical excision. Please send us the operation note and histology once it has been done.",
            t.Past(t.Ago(1, 15, 30)),
            [
                new LetterRecipientCommandItem(nameof(LetterRecipientKind.Hospital), null, "Riverside General Hospital (synthetic), Dermatology department", "1 Riverside Way\nExampleton", "ZZ3 3ZZ", null),
                new LetterRecipientCommandItem(nameof(LetterRecipientKind.GpPractice), gp.Id, null, null, null, null),
            ]);

        // Her results visit this morning: the outcome recorded, so Refer to hospital is current, waiting for the letter.
        var results = t.LastPast(9, 30, 20);
        await VisitAsync(seed, harriet, HistologyResult, nadia, results, 20, ClinicRoom1, t.Ago(8, 12, 0), attended: false);
        await StartAsync(seed, harriet, HistologyResult, results);
        await AttendedAsync(seed, harriet, HistologyResult, results.AddMinutes(21));
        await FinishAsync(seed, harriet, HistologyResult, results.AddMinutes(25), "Low-risk BCC");
        seed.Stories[HarrietQuill] = harriet;
    }

    /// <summary>
    /// Felix Moreton (branch to choose): seen yesterday, his Dermoscopy assessment started and its outcome not yet
    /// recorded; Dermatology asked the lab for an opinion first (a consult, open, MC25).
    /// </summary>
    private static async Task FelixMoretonAsync(DemoSeed seed)
    {
        var t = seed.Timeline;
        var nadia = DemoPeople.Thornbury;
        var felix = await CreateAsync(seed, FelixMoreton, SuspectedSkinCancer, "Suspicious lesion, right shin", 35, "High", t.Ago(6, 9, 15), nadia);
        seed.Tour["branchToChoose"] = felix.Id;
        await DoAsync(seed, felix, Triage, t.Ago(6, 9, 30));
        var visit = DemoTimeline.At(t.WeekdayBack(1), 14, 0);
        await VisitAsync(seed, felix, Dermoscopy, nadia, visit, 30, ClinicRoom1, t.Ago(5, 12, 0));
        await StartAsync(seed, felix, Dermoscopy, visit);

        var consult = await seed.Send(visit.AddMinutes(150), nadia, new RequestConsultCommand(felix.Id, Team.LabId,
            "Would a 4 mm punch biopsy give a reliable report on this 9 mm lesion, or should we refer for excision at the hospital?", nadia.Actor),
            "ask the lab for a consult");
        seed.Tour["consult"] = consult.Id;
        seed.Stories[FelixMoreton] = felix;
    }

    /// <summary>
    /// Aisha Khoury (forked tests): a second lesion at her skin check, so "Biopsy another lesion" was added (MC10);
    /// both biopsies taken, so both Histology stages are the lab's side by side. The second sample was too small: the
    /// lab recorded it insufficient, which closed its case asking for a repeat and reopened that Take biopsy (MC22).
    /// </summary>
    private static async Task AishaKhouryAsync(DemoSeed seed)
    {
        var t = seed.Timeline;
        var nadia = DemoPeople.Thornbury;
        var aisha = await CreateAsync(seed, AishaKhoury, SuspectedSkinCancer, "Two lesions, left shin", 30, "Medium", t.Ago(20, 9, 15), nadia);
        seed.Tour["forkedTests"] = aisha.Id;
        await DoAsync(seed, aisha, Triage, t.Ago(20, 9, 30));

        var check = DemoTimeline.At(t.WeekdayBack(9), 10, 0);
        await VisitAsync(seed, aisha, Dermoscopy, nadia, check, 30, ClinicRoom1, t.Ago(18, 12, 0));
        await StartAsync(seed, aisha, Dermoscopy, check);
        await FinishAsync(seed, aisha, Dermoscopy, check.AddMinutes(35), UncertainBiopsy);
        var branch = aisha.Latest.AddableBranches.Single(b => b.Name == "Biopsy another lesion");
        aisha.Latest = await seed.Send(check.AddMinutes(60), nadia, new AddPathwayBranchCommand(aisha.Id, branch.TemplateBranchId, aisha.Stage(Dermoscopy).Id,
            "A second lesion at the skin check.", nadia.Actor), "add a branch to Aisha Khoury's pathway");

        // The second lesion's biopsy, then its sample to the lab, too small to test.
        var secondDay = t.WeekdayBack(7) > DateOnly.FromDateTime(check) ? t.WeekdayBack(7) : NextWeekday(DateOnly.FromDateTime(check));
        var second = DemoTimeline.At(secondDay, 9, 30);
        await VisitAsync(seed, aisha, TakeBiopsy, nadia, second, 30, ProcedureRoom, check.AddMinutes(70), branch: true, attended: false);
        await ConsentAsync(seed, aisha, TakeBiopsy, AdultForm, second.AddMinutes(-10), branch: true);
        await StartAsync(seed, aisha, TakeBiopsy, second, branch: true);
        var sample = await SampleAsync(seed, aisha, TakeBiopsy, SkinBiopsy, SkinHistology, second.AddMinutes(10), "Left shin, lower lesion", null, branch: true);
        await AttendedAsync(seed, aisha, TakeBiopsy, second.AddMinutes(31), branch: true);
        await FinishAsync(seed, aisha, TakeBiopsy, second.AddMinutes(35), branch: true);
        await SendSampleAsync(seed, sample.Id, second.AddMinutes(250), nadia);
        var received = second.AddMinutes(10).AddDays(1);
        await ReceiveSampleAsync(seed, sample.Id, received);
        var labCase = await CaseOfAsync(seed, sample.Id);
        seed.Tour["labCaseRepeatRequested"] = labCase;
        await WorkCaseAsync(seed, labCase, received, received.AddHours(6),
            LabWork.Accession, new LabWork(Choice: "SampleAdequate", Outcome: "Insufficient", Detail: "Too little tissue to section."));

        // The first lesion's biopsy, taken two days ago: its Histology waits for its sample.
        var main = DemoTimeline.At(t.WeekdayBack(2), 10, 0);
        if (main <= second.AddMinutes(30))
            main = DemoTimeline.At(NextWeekday(secondDay), 10, 0);
        await VisitAsync(seed, aisha, TakeBiopsy, nadia, main, 30, ProcedureRoom, check.AddMinutes(75), attended: false);
        await ConsentAsync(seed, aisha, TakeBiopsy, AdultForm, main.AddMinutes(-10));
        await StartAsync(seed, aisha, TakeBiopsy, main);
        await AttendedAsync(seed, aisha, TakeBiopsy, main.AddMinutes(31));
        await FinishAsync(seed, aisha, TakeBiopsy, main.AddMinutes(35));
        seed.Stories[AishaKhoury] = aisha;
    }

    /// <summary>
    /// Callum Ashby (back from the lab): the lab reported his biopsy and finished his Histology, which handed the pathway
    /// back to Dermatology; its result awaits Nadia Thornbury's sign-off, so Histology result waits for it.
    /// </summary>
    private static async Task CallumAshbyAsync(DemoSeed seed)
    {
        var t = seed.Timeline;
        var nadia = DemoPeople.Thornbury;
        var callum = await CreateAsync(seed, CallumAshby, SuspectedSkinCancer, "Scaly patch, back of the right hand", 24, "Medium", t.Ago(21, 9, 15), nadia);
        seed.Tour["backFromLab"] = callum.Id;
        await DoAsync(seed, callum, Triage, t.Ago(21, 9, 30));

        var check = DemoTimeline.At(t.WeekdayBack(16), 11, 0);
        await VisitAsync(seed, callum, Dermoscopy, nadia, check, 30, ClinicRoom1, t.Ago(20, 12, 0));
        await StartAsync(seed, callum, Dermoscopy, check);
        await FinishAsync(seed, callum, Dermoscopy, check.AddMinutes(35), UncertainBiopsy);

        var biopsy = DemoTimeline.At(t.WeekdayBack(10), 14, 0);
        await VisitAsync(seed, callum, TakeBiopsy, nadia, biopsy, 30, ProcedureRoom, check.AddMinutes(60), attended: false);
        await ConsentAsync(seed, callum, TakeBiopsy, AdultForm, biopsy.AddMinutes(-10));
        await StartAsync(seed, callum, TakeBiopsy, biopsy);
        var sample = await SampleAsync(seed, callum, TakeBiopsy, SkinBiopsy, SkinHistology, biopsy.AddMinutes(20), "Back of the right hand", null);
        await AttendedAsync(seed, callum, TakeBiopsy, biopsy.AddMinutes(31));
        await FinishAsync(seed, callum, TakeBiopsy, biopsy.AddMinutes(35));
        await SendSampleAsync(seed, sample.Id, biopsy.AddMinutes(200), nadia);
        var received = biopsy.AddMinutes(20).AddDays(1);
        await ReceiveSampleAsync(seed, sample.Id, received, "EVP-55187");
        await StartAsync(seed, callum, Histology, received.AddMinutes(5), DemoPeople.Aldous);

        var reportedAt = DemoTimeline.At(t.WeekdayBack(5), 15, 0);
        var labCase = await CaseOfAsync(seed, sample.Id);
        await WorkCaseAsync(seed, labCase, received.AddMinutes(5), reportedAt.AddMinutes(-30), [.. DemoReplay.Histology, LabWork.NoMoreWork]);
        await AuthoriseAsync(seed, labCase, reportedAt, "Seborrhoeic keratosis, no dysplasia.");
        await FinishAsync(seed, callum, Histology, reportedAt.AddMinutes(5), by: DemoPeople.Aldous);
        seed.Tour["resultToSignOff"] = await seed.Db(db => db.Results.Where(r => r.SpecimenId == sample.Id).Select(r => r.Id).SingleAsync(seed.Ct));
        seed.Stories[CallumAshby] = callum;
    }

    /// <summary>Nora Fennick (photographs to take): the dermoscopy was uncertain, so Take photographs is next, which needs her photo consent (MC27).</summary>
    private static async Task NoraFennickAsync(DemoSeed seed)
    {
        var t = seed.Timeline;
        var nadia = DemoPeople.Thornbury;
        var nora = await CreateAsync(seed, NoraFennick, ChangingMole, "Atypical mole, left shoulder", 45, "Medium", t.Ago(12, 9, 15), nadia);
        seed.Tour["photosToTake"] = nora.Id;
        await DoAsync(seed, nora, Triage, t.Ago(12, 9, 30));
        var check = DemoTimeline.At(t.WeekdayBack(6), 11, 0);
        await VisitAsync(seed, nora, Dermoscopy, nadia, check, 30, ClinicRoom1, t.Ago(11, 12, 0));
        await StartAsync(seed, nora, Dermoscopy, check);
        await FinishAsync(seed, nora, Dermoscopy, check.AddMinutes(35), "Uncertain: monitor");
        seed.Stories[NoraFennick] = nora;
    }

    /// <summary>
    /// Leah Corrin (the tour's relay, the owner's three stages): seen at Triage yesterday afternoon, where a skin scraping
    /// was taken with her verbal agreement, never recorded, so Triage waits for her consent. Her sample is already with the
    /// lab, its case worked up to the results; once Triage is finished, the lab's Testing finds it.
    /// <para>
    /// Her pathway's Triage needs a visit and her consent, which the template's (shared) Triage doesn't: a pathway's stage
    /// has no command to change its gates, so the seed sets them on the stage, as the light demo's seed does.
    /// </para>
    /// </summary>
    private static async Task LeahCorrinAsync(DemoSeed seed)
    {
        var t = seed.Timeline;
        var nadia = DemoPeople.Thornbury;
        var leah = await CreateAsync(seed, LeahCorrin, DemoDermatology.SkinSampleToTheLab, "Scaly patch, left forearm", 21, "Medium", t.Ago(3, 9, 15), nadia);
        seed.Tour["relay"] = leah.Id;
        var triageId = leah.Stage(Triage).Id;
        await seed.Db(db => db.PatientPathwayStages.Where(s => s.Id == triageId).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.VisitRequirement, VisitRequirement.Required)
            .SetProperty(x => x.NeedsConsent, true), seed.Ct));

        var visit = DemoTimeline.At(t.WeekdayBack(1), 15, 0);
        await VisitAsync(seed, leah, Triage, nadia, visit, 30, ClinicRoom1, t.Ago(3, 9, 30), attended: false);
        await StartAsync(seed, leah, Triage, visit);
        var sample = await SampleAsync(seed, leah, Triage, SkinScraping, ScrapingMicroscopy, visit.AddMinutes(20), "Left forearm", null);
        await AttendedAsync(seed, leah, Triage, visit.AddMinutes(31));
        await SendSampleAsync(seed, sample.Id, visit.AddMinutes(45), nadia);
        var received = visit.AddMinutes(75);
        await ReceiveSampleAsync(seed, sample.Id, received);
        var labCase = await CaseOfAsync(seed, sample.Id);
        seed.Tour["labCaseRelay"] = labCase;
        await WorkCaseAsync(seed, labCase, received, received.AddMinutes(150),
            LabWork.Accession, LabWork.Adequate, LabWork.TestStep("Microscopy"), LabWork.TestStep("Report"), LabWork.NoMoreWork);
        seed.Stories[LeahCorrin] = leah;
    }

    /// <summary>Ilona Petrova's second pathway: seen this morning at Take skin sample, her scraping taken and not yet sent.</summary>
    private static async Task IlonaPetrovaScrapingAsync(DemoSeed seed)
    {
        var t = seed.Timeline;
        var nadia = DemoPeople.Thornbury;
        var ilona = await CreateAsync(seed, IlonaPetrova, SkinInfection, "Scaly patch, right elbow", 40, "Medium", t.Ago(20, 9, 45), nadia);
        seed.Tour["scrapingToSend"] = ilona.Id;
        await DoAsync(seed, ilona, Triage, t.Ago(20, 10, 0));
        var assessment = DemoTimeline.At(t.WeekdayBack(10), 11, 0);
        await VisitAsync(seed, ilona, "Infection assessment", nadia, assessment, 30, ClinicRoom1, t.Ago(18, 12, 0));
        await StartAsync(seed, ilona, "Infection assessment", assessment);
        await FinishAsync(seed, ilona, "Infection assessment", assessment.AddMinutes(35), "Sample needed");

        var visit = t.LastPast(9, 0, 15);
        await VisitAsync(seed, ilona, "Take skin sample", nadia, visit, 15, ClinicRoom1, visit.AddHours(-21));
        await StartAsync(seed, ilona, "Take skin sample", visit);
        var sample = await SampleAsync(seed, ilona, "Take skin sample", SkinScraping, FungalScraping, visit.AddMinutes(10), "Right elbow", null);
        seed.Tour["specimenToSend"] = sample.Id;
        seed.Stories["Ilona Petrova (scraping)"] = ilona;
    }

    /// <summary>Records a stage's booked visit attended, by the clinician it's with.</summary>
    internal static async Task AttendedAsync(DemoSeed seed, StoryPathway pathway, string stage, DateTime at, bool branch = false)
    {
        var stageId = pathway.Stage(stage, branch).Id;
        var booked = await seed.Db(db => db.Appointments
            .Where(a => a.StageId == stageId && a.Status == Domain.Appointments.AppointmentStatus.Booked)
            .OrderBy(a => a.StartUtc)
            .Select(a => new { a.Id, a.StaffUserId })
            .FirstAsync(seed.Ct));
        var staff = seed.People.Single(p => p.Id == booked.StaffUserId);
        await seed.Send(at, staff, new Application.Appointments.Commands.RecordAppointmentOutcome.RecordAppointmentOutcomeCommand(
            booked.Id, nameof(Domain.Appointments.AppointmentStatus.Attended), staff.Actor), $"record the '{stage}' visit attended on {pathway.Latest.Title}");
    }

    /// <summary>The booking coordinator posts an approved letter to every recipient.</summary>
    private static async Task PostAsync(DemoSeed seed, LetterResponse letter, DateTime at)
    {
        var marsh = DemoPeople.Marsh;
        foreach (var recipient in letter.Recipients.Where(r => r.DispatchStatus != nameof(LetterDispatchStatus.Sent)).ToList())
            letter = await seed.Send(at, marsh, new RecordLetterSentCommand(letter.Id, recipient.Id, nameof(LetterDispatchMethod.Post),
                Application.Calendar.Common.UkTime.DateOf(at), letter.Version, marsh.Actor), "record a letter sent");
    }
}
