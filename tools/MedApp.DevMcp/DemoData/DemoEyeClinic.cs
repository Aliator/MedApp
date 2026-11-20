using MedApp.Application.Referrals.Commands.RegisterReferral;
using static MedApp.DevMcp.DemoData.DemoOphthalmology;
using static MedApp.DevMcp.DemoData.DemoPatients;
using static MedApp.DevMcp.DemoData.DemoReplay;
using static MedApp.DevMcp.DemoData.DemoSetup;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// The Eye clinic's patients and pathways at the light demo's points (the owner, 20 November 2026), replayed through the
/// app's handlers as <see cref="DemoStory"/> replays Dermatology's: Saira Varma's cataract, glaucoma and red eye
/// pathways (Oliver Maddox's glaucoma beside his Dermatology referral, so the team walls show), Freya Tolland's eye swab
/// with the lab, Hamish Dewar's new pathway unassigned, and Ruth Penhaligon's referral awaiting triage. The service
/// manager assigns and the booking coordinator books and registers, as for Dermatology.
/// </summary>
public static class DemoEyeClinic
{
    private const string Triage = "Triage";

    public static async Task RunAsync(DemoSeed seed)
    {
        await RuthPenhaligonAsync(seed);
        await EdithMarlowAsync(seed);
        await OliverMaddoxAsync(seed);
        await FreyaTollandAsync(seed);
        await HamishDewarAsync(seed);
    }

    /// <summary>Ruth Penhaligon's referral from her GP practice, received two days ago, awaiting the Eye clinic's triage.</summary>
    private static async Task RuthPenhaligonAsync(DemoSeed seed)
    {
        var t = seed.Timeline;
        var marsh = DemoPeople.Marsh;
        var referral = await seed.Send(t.Ago(2, 11, 30), marsh, new RegisterReferralCommand(seed.Patients[RuthPenhaligon], seed.TeamId(DemoPeople.EyeClinic),
            t.DaysFrom(-2), ExampleLane, "Dr A. Sample", null, "High", "Suspected glaucoma, raised pressures at the optometrist", false, marsh.Actor,
            PracticeId: seed.Practices[ExampleLane]), "register Ruth Penhaligon's referral");
        seed.Tour["eyeReferralToTriage"] = referral.Id;
    }

    /// <summary>Edith Marlow (biometry booked): triaged, assessed about a week ago (she wants surgery and is suitable), so Biometry is booked on the next open day.</summary>
    private static async Task EdithMarlowAsync(DemoSeed seed)
    {
        var t = seed.Timeline;
        var saira = DemoPeople.Varma;
        var edith = await CreateAsync(seed, EdithMarlow, CataractAssessment, "Cataract, right eye", 45, "Medium", t.Ago(20, 9, 15), saira);
        seed.Tour["eyeBiometryBooked"] = edith.Id;
        await DoAsync(seed, edith, Triage, t.Ago(20, 9, 30));

        var assessment = DemoTimeline.At(t.WeekdayBack(7), 10, 0);
        await VisitAsync(seed, edith, CataractAssessment, saira, assessment, 30, ProcedureRoom, t.Ago(19, 12, 0));
        await StartAsync(seed, edith, CataractAssessment, assessment);
        await FinishAsync(seed, edith, CataractAssessment, assessment.AddMinutes(35), "Wants surgery and suitable");

        var biometry = DemoTimeline.At(t.OpenDayAhead(1), 10, 0);
        await VisitAsync(seed, edith, "Biometry", saira, biometry, 30, ProcedureRoom, assessment.AddMinutes(60), attended: false);
        seed.Stories[EdithMarlow] = edith;
    }

    /// <summary>
    /// Oliver Maddox (glaucoma assessment booked): triaged, his Glaucoma assessment booked at 14:00 today if that's still
    /// ahead on an open day, else on the next. His urgent Dermatology referral awaits triage beside it.
    /// </summary>
    private static async Task OliverMaddoxAsync(DemoSeed seed)
    {
        var t = seed.Timeline;
        var saira = DemoPeople.Varma;
        var oliver = await CreateAsync(seed, OliverMaddox, GlaucomaAssessment, "Raised eye pressure, both eyes", 50, "Medium", t.Ago(10, 9, 15), saira);
        seed.Tour["eyeGlaucomaBooked"] = oliver.Id;
        await DoAsync(seed, oliver, Triage, t.Ago(10, 9, 30));

        var day = t.IsOpen(t.Today) && DemoTimeline.At(t.Today, 14, 0) > t.NowUtc ? t.Today : t.OpenDayAhead(1);
        await VisitAsync(seed, oliver, GlaucomaAssessment, saira, DemoTimeline.At(day, 14, 0), 30, ProcedureRoom, t.Ago(9, 12, 0), attended: false);
        seed.Stories[OliverMaddox] = oliver;
    }

    /// <summary>
    /// Freya Tolland (swab with the lab): triaged the day she was referred, examined three days ago (a swab needed) and
    /// swabbed at the same visit; the lab received her swab the next day, picked up her Microbiology and accessioned its
    /// case, the sample adequate, so Culture is its next step.
    /// </summary>
    private static async Task FreyaTollandAsync(DemoSeed seed)
    {
        var t = seed.Timeline;
        var saira = DemoPeople.Varma;
        var freya = await CreateAsync(seed, FreyaTolland, RedEyeAssessment, "Red sore eye, left", 14, "High", t.Ago(5, 9, 15), saira);
        seed.Tour["eyeSwabAtLab"] = freya.Id;
        await DoAsync(seed, freya, Triage, t.Ago(5, 9, 30));

        // The examination, then the swab's own visit straight after it (each stage's gate needs its visit).
        var exam = DemoTimeline.At(t.WeekdayBack(3), 10, 0);
        await VisitAsync(seed, freya, "Eye examination", saira, exam, 15, ProcedureRoom, t.Ago(5, 9, 50));
        await StartAsync(seed, freya, "Eye examination", exam);
        await FinishAsync(seed, freya, "Eye examination", exam.AddMinutes(17), "Swab needed");
        var swab = exam.AddMinutes(20);
        await VisitAsync(seed, freya, "Take eye swab", saira, swab, 10, ProcedureRoom, exam.AddMinutes(18), attended: false);
        await StartAsync(seed, freya, "Take eye swab", swab);
        var sample = await SampleAsync(seed, freya, "Take eye swab", EyeSwab, EyeSwabCulture, exam.AddMinutes(25), "Left eye", null);
        await DemoStory.AttendedAsync(seed, freya, "Take eye swab", swab.AddMinutes(11));
        await FinishAsync(seed, freya, "Take eye swab", swab.AddMinutes(12));
        seed.Tour["eyeSwab"] = sample.Id;

        await SendSampleAsync(seed, sample.Id, exam.AddMinutes(145), saira);
        var received = t.Past(exam.AddMinutes(30).AddDays(1));
        await ReceiveSampleAsync(seed, sample.Id, received, "MB-30416");
        await StartAsync(seed, freya, "Microbiology", t.Past(received.AddMinutes(5)), DemoPeople.Aldous);
        var labCase = await CaseOfAsync(seed, sample.Id);
        seed.Tour["eyeLabCase"] = labCase;
        await WorkCaseAsync(seed, labCase, received, t.Past(received.AddHours(2)), LabWork.Accession, LabWork.Adequate);
        seed.Stories[FreyaTolland] = freya;
    }

    /// <summary>Hamish Dewar (unassigned): a new pathway at Triage, started by Saira Varma yesterday, not yet assigned.</summary>
    private static async Task HamishDewarAsync(DemoSeed seed)
    {
        var t = seed.Timeline;
        var hamish = await CreateAsync(seed, HamishDewar, MacularAssessment, "Distorted central vision, right eye", 21, "High", t.Ago(1, 9, 15), DemoPeople.Varma, assign: false);
        seed.Tour["eyeUnassigned"] = hamish.Id;
        seed.Stories[HamishDewar] = hamish;
    }
}
