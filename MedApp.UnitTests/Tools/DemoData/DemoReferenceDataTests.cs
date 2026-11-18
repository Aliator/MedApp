using FluentAssertions;
using MedApp.Application.Attachments.Common;
using MedApp.DevMcp.DemoData;
using MedApp.Domain.Patients;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Terminology;
using static MedApp.UnitTests.Common.Constants.DemoDataTestConstants;

namespace MedApp.UnitTests.Tools.DemoData;

/// <summary>The demo's fixed data: stage definitions, pathways, NHS numbers and the files it attaches.</summary>
[TestFixture]
public sealed class DemoReferenceDataTests
{
    [Test]
    public void Definitions_SnomedCodes_AreValidConceptIdsWithTerms_AndSomeAreLeftBlank()
    {
        var coded = DemoPathwayKinds.Definitions.Where(d => d.SnomedCode is not null).ToList();

        coded.Should().OnlyContain(d => SnomedCt.IsConceptId(d.SnomedCode) && !string.IsNullOrWhiteSpace(d.SnomedTerm));
        DemoPathwayKinds.Definitions.Should().Contain(d => d.SnomedCode == null && !d.IsArchived);
    }

    [Test]
    public void PathwayKinds_EveryStage_NamesADefinitionThatIsntArchived()
    {
        var active = DemoPathwayKinds.Definitions.Where(d => !d.IsArchived).Select(d => d.Name).ToHashSet();

        DemoPathwayKinds.PathwayKinds.SelectMany(p => p.Stages).Should().OnlyContain(s => active.Contains(s.Definition));
    }

    [Test]
    public void PathwayKinds_StagesWithAVisitHaveAVisitLength_AndOnlyThose()
    {
        var visits = DemoPathwayKinds.Definitions.ToDictionary(d => d.Name, d => d.Visit);

        foreach (var stage in DemoPathwayKinds.PathwayKinds.SelectMany(p => p.Stages).Where(s => s.VisitMinutes > 0))
            visits[stage.Definition].Should().NotBe(VisitRequirement.None, stage.Definition);
        foreach (var stage in DemoPathwayKinds.PathwayKinds.SelectMany(p => p.Stages)
                     .Where(s => visits[s.Definition] == VisitRequirement.Required))
            stage.VisitMinutes.Should().BePositive($"{stage.Definition} needs a visit, so the seed books one");
    }

    /// <summary>Every definition is used by a pathway, but the retired one; and the archived pathway is the only archived one.</summary>
    [Test]
    public void PathwayKinds_UseEveryLiveDefinition_AndOneIsArchived()
    {
        var used = DemoPathwayKinds.PathwayKinds.SelectMany(p => p.Stages).Select(s => s.Definition).ToHashSet();

        DemoPathwayKinds.Definitions.Where(d => !d.IsArchived).Select(d => d.Name).Should().BeEquivalentTo(used);
        DemoPathwayKinds.Definitions.Should().ContainSingle(d => d.IsArchived);
        DemoPathwayKinds.PathwayKinds.Should().ContainSingle(p => p.IsArchived).Which.PathwayKind.Should().Be(PathwayKind.SkinLesion2024);
        DemoPathwayKinds.PathwayKinds.Select(p => p.TemplateName).Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// Visits are needed where the patient must be seen, suggested where a call often does, and never for the lab's work
    /// or a letter.
    /// </summary>
    [Test]
    public void Definitions_VisitRequirements_FitTheWork()
    {
        var visits = DemoPathwayKinds.Definitions.ToDictionary(d => d.Name, d => d.Visit);

        new[] { DemoPathwayKinds.FirstClinicVisit, DemoPathwayKinds.BiopsyOrSample, DemoPathwayKinds.Treatment, DemoPathwayKinds.BloodAndUrineTests, DemoPathwayKinds.ClinicReview, DemoPathwayKinds.BiomechanicsSession }
            .Should().OnlyContain(name => visits[name] == VisitRequirement.Required);
        new[] { DemoPathwayKinds.FollowUp, DemoPathwayKinds.ResultsAndTeamReview, DemoPathwayKinds.MedicinesReview }
            .Should().OnlyContain(name => visits[name] == VisitRequirement.Suggested);
        new[] { DemoPathwayKinds.ReferralReceived, DemoPathwayKinds.Triage, DemoPathwayKinds.Histology, DemoPathwayKinds.LabTests, DemoPathwayKinds.DischargeLetter }
            .Should().OnlyContain(name => visits[name] == VisitRequirement.None);
    }

    /// <summary>The two pathways that see only women, or only men, see only adults old enough; children only where the story says.</summary>
    [Test]
    public void Sees_TheTwoSingleSexPathways_SeeOnlyOlderAdults()
    {
        ProcedureKinds.Sees(PathwayKind.PostMenopausalBleeding).Should().Be((true, 50));
        ProcedureKinds.Sees(PathwayKind.Prostate).Should().Be((false, 50));
        ProcedureKinds.Sees(PathwayKind.Diabetes).Should().Be(((bool?)null, 0));
        Enum.GetValues<PathwayKind>().Where(ProcedureKinds.SeesChildren).Should().BeEquivalentTo([PathwayKind.InflammatorySkin, PathwayKind.Biomechanics]);
    }

    [Test]
    public void SyntheticNhsNumber_IsValidAndInThe999TestRange()
    {
        var numbers = Enumerable.Range(0, 300).Select(DemoPatients.SyntheticNhsNumber).ToList();

        numbers.Should().OnlyHaveUniqueItems();
        numbers.Should().OnlyContain(n => n.StartsWith("999") && NhsNumbers.IsValid(n) && !NhsNumbers.IsPlaceholder(n));
    }

    [Test]
    public void SyntheticFiles_AreRecognisedByTheAppsOwnTypeSniffer()
    {
        var random = new Random(Seed);

        Sniff(SyntheticFiles.Pdf("Report", ["Line one", "Line (two)"]), "Report.pdf").Should().Be(AttachmentFileTypes.Pdf);
        Sniff(SyntheticFiles.FlowTracePng(12, random), "Flow test.png").Should().Be(AttachmentFileTypes.Png);
        Sniff(SyntheticFiles.DiagramPng("Home programme", ["Stretch", "Strength"]), "Home programme.png").Should().Be(AttachmentFileTypes.Png);
        foreach (var site in DemoPathwayKinds.PathwayKinds.SelectMany(p => ProcedureKinds.For(p.PathwayKind)).SelectMany(k => k.Sites).Distinct())
            Sniff(SyntheticFiles.SiteMapPng(site), "Lesion site map.png").Should().Be(AttachmentFileTypes.Png, site);
        Sniff(SyntheticFiles.Text("Note", ["Called the patient."]), "Note.txt").ContentType.Should().StartWith("text/plain");

        static AttachmentFileType Sniff(byte[] bytes, string name) => FileTypeSniffer.Identify(new MemoryStream(bytes), name);
    }

    [Test]
    public void SyntheticFiles_PdfHasNoActiveContentAndSaysItsSynthetic()
    {
        var text = System.Text.Encoding.ASCII.GetString(SyntheticFiles.Pdf("Report", ["Line"]));

        FileTypeSniffer.PdfActiveContent.Should().OnlyContain(marker => !text.Contains(marker));
        text.Should().Contain(SyntheticFiles.Banner).And.EndWith("%%EOF\n");
    }
}
