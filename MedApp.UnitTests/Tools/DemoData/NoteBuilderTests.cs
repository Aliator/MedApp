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

    [TestCaseSource(nameof(StageNames))]
    public void StageNote_EveryStageOfEveryPathway_IsASentenceToAShortParagraph(string stage)
    {
        foreach (var pathway in DemoPathways.Pathways.Where(p => p.Stages.Any(s => s.Definition == stage)))
        foreach (var kind in ProcedureKinds.For(pathway.Pathway).Distinct())
        {
            var note = NoteBuilder.StageNote(stage, new NoteContext(kind, null, false), _random);

            note.Should().NotBeNullOrWhiteSpace();
            note.Length.Should().BeInRange(20, 900);
            note.Should().NotContain("{").And.NotContain("  ").And.NotStartWith(" ");
        }
    }

    [Test]
    public void StageNote_SameStageAndKind_RarelyReadsTheSame()
    {
        var notes = Enumerable.Range(0, Samples)
            .Select(_ => NoteBuilder.StageNote("Pre-assessment", new NoteContext(Knee, "left", false), _random))
            .ToList();

        notes.Distinct().Count().Should().BeGreaterThan(Samples * 9 / 10);
    }

    [Test]
    public void StageNote_DependsOnTheKindOfProcedure()
    {
        var colonoscopy = Enumerable.Range(0, Samples).Select(_ => NoteBuilder.StageNote("Procedure", new NoteContext(Colonoscopy, null, false), _random));
        var knee = Enumerable.Range(0, Samples).Select(_ => NoteBuilder.StageNote("Procedure", new NoteContext(Knee, "right", false), _random));

        colonoscopy.Should().OnlyContain(n => n.Contains("colonoscopy", StringComparison.OrdinalIgnoreCase) || n.Contains("caecum"));
        knee.Should().OnlyContain(n => n.Contains("Right knee replacement"));
    }

    [Test]
    public void StageNote_SometimesUsesMarkdown()
    {
        var notes = Enumerable.Range(0, Samples * 3)
            .Select(_ => NoteBuilder.StageNote("Pre-assessment", new NoteContext(Knee, "left", false), _random))
            .ToList();

        notes.Should().Contain(n => n.Contains("\n- "), "some notes list their checks");
        notes.Should().Contain(n => n.Contains("**"), "some notes make a key point bold");
        notes.Should().Contain(n => !n.Contains('\n') && !n.Contains("**"), "most are plain sentences");
    }

    [Test]
    public void StageNote_PhysiotherapySession_SaysWhichSessionOfHowMany()
    {
        var note = NoteBuilder.StageNote("Physiotherapy session", new NoteContext(ProcedureKinds.ShoulderRehab, "left", false, 2, 4), _random);

        note.Should().StartWith("Session 2 of 4.");
    }

    [Test]
    public void StageNote_UrgentReferral_IsFlagged()
    {
        var note = NoteBuilder.StageNote("Referral received", new NoteContext(Colonoscopy, null, Urgent: true), _random);

        note.Should().Contain("Two-week-wait referral");
    }

    [Test]
    public void Correction_KeepsTheOriginalAndAddsABoldCorrection()
    {
        var note = NoteBuilder.Correction("Original note.", new NoteContext(Knee, "left", false), _random);

        note.Should().StartWith("Original note.\n\n**Correction:** ");
    }

    [Test]
    public void Notes_NeverNameThePatient()
    {
        var notes = StageNames()
            .SelectMany(stage => Enumerable.Range(0, 10).Select(_ => NoteBuilder.StageNote(stage, new NoteContext(Knee, "left", false), _random)))
            .Concat(Enumerable.Range(0, 10).Select(_ => NoteBuilder.ProcedureNote(new NoteContext(Colonoscopy, null, false), _random)))
            .ToList();

        notes.Should().NotContain(n => n.Contains("Mr ") || n.Contains("Mrs ") || n.Contains("Ms ") || n.Contains("Miss "));
    }

    [Test]
    public void Reasons_AreVaried()
    {
        Enumerable.Range(0, Samples).Select(_ => NoteBuilder.BlockReason(_random)).Distinct().Count().Should().BeGreaterThan(3);
        Enumerable.Range(0, Samples).Select(_ => NoteBuilder.CancelReason(_random)).Distinct().Count().Should().BeGreaterThan(3);
        Enumerable.Range(0, Samples).Select(_ => NoteBuilder.AppointmentNote(_random)).Distinct().Count().Should().BeGreaterThan(3);
    }
}
