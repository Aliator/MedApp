using FluentAssertions;
using MedApp.DevMcp.DemoData;
using static MedApp.UnitTests.Common.Constants.DemoDataTestConstants;

namespace MedApp.UnitTests.Tools.DemoData;

[TestFixture]
public sealed class NoteBuilderTests
{
    private Random _random = null!;

    [SetUp]
    public void SetUp() => _random = new Random(Seed);

    private static IEnumerable<string> StageNames() =>
        DemoPathways.Pathways.SelectMany(p => p.Stages).Select(s => s.Definition).Distinct();

    private static IEnumerable<ProcedureKind> Kinds() =>
        DemoPathways.Pathways.SelectMany(p => ProcedureKinds.For(p.Pathway)).Distinct();

    [TestCaseSource(nameof(StageNames))]
    public void StageNote_EveryStageOfEveryPathway_IsASentenceToAShortParagraph(string stage)
    {
        foreach (var pathway in DemoPathways.Pathways.Where(p => p.Stages.Any(s => s.Definition == stage)))
        foreach (var kind in ProcedureKinds.For(pathway.Pathway).Distinct())
        for (var outcome = 0; outcome < kind.Outcomes.Length; outcome++)
        {
            var site = kind.Sites.Length > 0 ? kind.Sites[0] : null;
            var note = NoteBuilder.StageNote(stage, new NoteContext(kind, site, false, Outcome: outcome), _random);

            note.Should().NotBeNullOrWhiteSpace();
            note.Length.Should().BeInRange(20, 900);
            note.Should().NotContain("{").And.NotContain("  ").And.NotStartWith(" ").And.NotContain("..").And.NotContain(" .");
        }
    }

    [TestCaseSource(nameof(StageNames))]
    public void ProgressNote_EveryStage_SaysWhatItsWaitingFor(string stage)
    {
        var note = NoteBuilder.ProgressNote(stage, new NoteContext(KindWithASite, "left forearm", false, 2, 3), _random);

        note.Should().NotBeNullOrWhiteSpace().And.NotContain("{");
    }

    /// <summary>Each pathway's kinds have what its stages' notes are built from, so no note falls back to a stock line.</summary>
    [Test]
    public void Kinds_HaveWhatTheirPathwaysStagesNeed()
    {
        foreach (var pathway in DemoPathways.Pathways)
        foreach (var kind in ProcedureKinds.For(pathway.Pathway).Distinct())
        {
            var stages = pathway.Stages.Select(s => s.Definition).ToHashSet();
            var why = $"{kind.Title} on {pathway.TemplateName}";

            kind.Indications.Should().NotBeEmpty(why);
            kind.Outcomes.Should().NotBeEmpty(why).And.OnlyContain(o => o.Seen.Length > 0 && o.Plan.Length > 0, why);
            if (stages.Contains(DemoPathways.Histology) || stages.Contains(DemoPathways.LabTests) || stages.Contains(DemoPathways.ResultsAndTeamReview))
                kind.Outcomes.Should().OnlyContain(o => o.Result.Length > 0, why);
            if (stages.Contains(DemoPathways.BiopsyOrSample) || stages.Contains(DemoPathways.BloodAndUrineTests))
                kind.Sample.Should().NotBeEmpty(why);
            if (stages.Contains(DemoPathways.BiopsyOrSample))
                kind.SampleNotes.Should().NotBeEmpty(why);
            if (stages.Contains(DemoPathways.ResultsAndTeamReview))
                kind.Team.Should().NotBeEmpty(why);
            if (stages.Contains(DemoPathways.Treatment))
                kind.Outcomes.Should().OnlyContain(o => o.Treatment.Length > 0, why);
            if (stages.Contains(DemoPathways.FollowUp))
                kind.FollowUps.Should().NotBeEmpty(why);
            if (stages.Contains(DemoPathways.MedicinesReview))
                kind.Medicines.Should().NotBeEmpty(why);
        }
    }

    [Test]
    public void StageNote_SameStageAndKind_RarelyReadsTheSame()
    {
        var notes = Enumerable.Range(0, Samples)
            .Select(_ => NoteBuilder.StageNote(DemoPathways.ClinicReview, new NoteContext(KindWithoutASite, null, false), _random))
            .ToList();

        notes.Distinct().Count().Should().BeGreaterThan(Samples * 9 / 10);
    }

    [Test]
    public void StageNote_DependsOnTheKindOfReferral()
    {
        var lesion = Enumerable.Range(0, Samples).Select(_ => NoteBuilder.StageNote(DemoPathways.BiopsyOrSample, new NoteContext(KindWithASite, "right calf", false), _random));
        var bleeding = Enumerable.Range(0, Samples).Select(_ => NoteBuilder.StageNote(DemoPathways.BiopsyOrSample, new NoteContext(ProcedureKinds.PostMenopausalBleed, null, false), _random));

        lesion.Should().OnlyContain(n => n.StartsWith("4 mm punch biopsy taken from the right calf."));
        bleeding.Should().OnlyContain(n => n.StartsWith("Endometrial sample taken."));
    }

    /// <summary>One referral's notes agree: every stage names the same site, and the lab's result is the one the letter gives.</summary>
    [Test]
    public void StageNote_OneReferral_KeepsItsSiteAndItsOutcomeFromStageToStage()
    {
        for (var outcome = 0; outcome < KindWithASite.Outcomes.Length; outcome++)
        {
            var context = new NoteContext(KindWithASite, "left temple", false, Outcome: outcome);
            var expected = KindWithASite.Outcomes[outcome];
            string Note(string stage) => NoteBuilder.StageNote(stage, context, _random);

            Note(DemoPathways.FirstClinicVisit).Should().Contain("Site: left temple.").And.Contain(expected.Seen);
            Note(DemoPathways.BiopsyOrSample).Should().Contain("from the left temple");
            Note(DemoPathways.Histology).Should().Contain(expected.Result);
            Note(DemoPathways.ResultsAndTeamReview).Should().Contain(expected.Result).And.Contain("the skin cancer team meeting");
            Note(DemoPathways.Treatment).Should().Contain("Site: left temple.").And.Contain(expected.Treatment);
            Note(DemoPathways.DischargeLetter).Should().Contain(expected.Result);
        }
    }

    [Test]
    public void StageNote_SometimesUsesMarkdown()
    {
        var notes = Enumerable.Range(0, Samples * 3)
            .Select(_ => NoteBuilder.StageNote(DemoPathways.FirstClinicVisit, new NoteContext(KindWithASite, "left forearm", false), _random))
            .ToList();

        notes.Should().Contain(n => n.Contains("\n- "), "some notes list their plan");
        notes.Should().Contain(n => n.Contains("**"), "some notes make a key point bold");
        notes.Should().Contain(n => !n.Contains('\n') && !n.Contains("**"), "most are plain sentences");
    }

    [Test]
    public void StageNote_BiomechanicsSession_SaysWhichSessionOfHowMany()
    {
        var note = NoteBuilder.StageNote(DemoPathways.BiomechanicsSession, new NoteContext(ProcedureKinds.HeelPain, "left heel", false, 2, 3), _random);

        note.Should().StartWith("Session 2 of 3.");
    }

    [Test]
    public void StageNote_UrgentReferral_IsFlagged()
    {
        var note = NoteBuilder.StageNote(DemoPathways.ReferralReceived, new NoteContext(KindWithASite, null, Urgent: true), _random);

        note.Should().Contain("**Urgent referral:**");
    }

    [Test]
    public void Correction_KeepsTheOriginalAndAddsABoldCorrection()
    {
        var note = NoteBuilder.Correction("Original note.", new NoteContext(KindWithASite, "left forearm", false), _random);

        note.Should().StartWith("Original note.\n\n**Correction:** ");
    }

    /// <summary>
    /// Notes say "the patient", never a name or a title; and never "procedure", which is the app's old word for a
    /// pathway record: clinical text names the biopsy, the sample or the treatment instead.
    /// </summary>
    [Test]
    public void Notes_NeverNameThePatient_NorSayProcedure()
    {
        var notes = StageNames()
            .SelectMany(stage => Kinds().SelectMany(kind => Enumerable.Range(0, 5)
                .Select(i => NoteBuilder.StageNote(stage, new NoteContext(kind, kind.Sites.FirstOrDefault(), i == 0, Outcome: i), _random))))
            .Concat(Kinds().SelectMany(kind => Enumerable.Range(0, 10).Select(_ => NoteBuilder.ProcedureNote(new NoteContext(kind, null, false), _random))))
            .Concat(Enumerable.Range(0, Samples).SelectMany(_ => new[]
            {
                NoteBuilder.BlockReason(_random), NoteBuilder.CancelReason(_random), NoteBuilder.BypassReason(_random),
                NoteBuilder.AppointmentNote(_random), NoteBuilder.PatientCancelReason(_random), NoteBuilder.ClinicianCancelReason(_random),
                NoteBuilder.ProcedureNoteUpdate(string.Empty, _random), NoteBuilder.ProcedureCancelledAppointmentReason,
            }))
            .Concat(Enum.GetValues<StageRole>().SelectMany(role => Enumerable.Range(0, 10)
                .Select(_ => NoteBuilder.ProcedureFollowUpNote(DemoPathways.Triage, role, _random))))
            .ToList();

        notes.Should().NotContain(n => n.Contains("Mr ") || n.Contains("Mrs ") || n.Contains("Ms ") || n.Contains("Miss "));
        notes.Should().NotContain(n => n.Contains("procedure", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public void Reasons_AreVaried()
    {
        Enumerable.Range(0, Samples).Select(_ => NoteBuilder.BlockReason(_random)).Distinct().Count().Should().BeGreaterThan(3);
        Enumerable.Range(0, Samples).Select(_ => NoteBuilder.CancelReason(_random)).Distinct().Count().Should().BeGreaterThan(3);
        Enumerable.Range(0, Samples).Select(_ => NoteBuilder.AppointmentNote(_random)).Distinct().Count().Should().BeGreaterThan(3);
    }
}
