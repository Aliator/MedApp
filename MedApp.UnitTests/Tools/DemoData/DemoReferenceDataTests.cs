using FluentAssertions;
using MedApp.Application.Attachments.Common;
using MedApp.DevMcp.DemoData;
using MedApp.Domain.Patients;
using MedApp.Domain.Procedures.PatientProcedures;
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
        var coded = DemoPathways.Definitions.Where(d => d.SnomedCode is not null).ToList();

        coded.Should().OnlyContain(d => SnomedCt.IsConceptId(d.SnomedCode) && !string.IsNullOrWhiteSpace(d.SnomedTerm));
        DemoPathways.Definitions.Should().Contain(d => d.SnomedCode == null && !d.IsArchived);
    }

    [Test]
    public void Pathways_EveryStage_NamesADefinitionThatIsntArchived()
    {
        var active = DemoPathways.Definitions.Where(d => !d.IsArchived).Select(d => d.Name).ToHashSet();

        DemoPathways.Pathways.SelectMany(p => p.Stages).Should().OnlyContain(s => active.Contains(s.Definition));
    }

    [Test]
    public void Pathways_StagesWithAVisitHaveAVisitLength_AndOnlyThose()
    {
        var visits = DemoPathways.Definitions.ToDictionary(d => d.Name, d => d.Visit);

        foreach (var stage in DemoPathways.Pathways.SelectMany(p => p.Stages).Where(s => s.VisitMinutes > 0))
            visits[stage.Definition].Should().NotBe(VisitRequirement.None, stage.Definition);
        foreach (var stage in DemoPathways.Pathways.SelectMany(p => p.Stages)
                     .Where(s => visits[s.Definition] == VisitRequirement.Required))
            stage.VisitMinutes.Should().BePositive($"{stage.Definition} needs a visit, so the seed books one");
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
        Sniff(SyntheticFiles.TracePng(72, random), "ECG.png").Should().Be(AttachmentFileTypes.Png);
        Sniff(SyntheticFiles.DiagramPng(["Range", "Strength"]), "Plan.png").Should().Be(AttachmentFileTypes.Png);
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
