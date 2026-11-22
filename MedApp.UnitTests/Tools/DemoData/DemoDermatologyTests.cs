using FluentAssertions;
using MedApp.DevMcp.DemoData;
using MedApp.Domain.Letters;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.DemoDataTestConstants;

namespace MedApp.UnitTests.Tools.DemoData;

/// <summary>
/// Dermatology's templates as the demo data makes them (<see cref="DemoDermatology"/>), mapped onto the model by
/// <see cref="DemoTemplates"/> as the light demo maps them: the reviewed ten and the tour's relay, routes only forward,
/// shared definitions.
/// </summary>
[TestFixture]
public sealed class DemoDermatologyTests
{
    private static readonly IReadOnlyList<DemoTemplate> Templates = DemoTemplates.Templates(DemoDermatology.Clinic);

    private static DemoTemplate Template(string name) => Templates.Single(t => t.Name == name);

    [Test]
    public void Templates_AreTheReviewedTen_ThenTheToursRelay()
    {
        Templates.Select(t => t.Name).Should().Equal(
            "Suspected skin cancer", "Changing mole or lesion check", "Skin cancer surveillance", "Inflammatory skin assessment",
            "Eczema assessment", "Psoriasis assessment", "Acne assessment", "Suspected skin infection", "Hair loss assessment",
            "Skin pigmentation assessment", DemoDermatology.SkinSampleToTheLab);
    }

    [Test]
    public void Definitions_AreOnePerStageName_AndCoverEveryStage()
    {
        var definitions = DemoTemplates.Definitions();

        definitions.Select(d => d.Name).Should().OnlyHaveUniqueItems();
        definitions[0].Name.Should().Be(DemoTemplates.CarePlan, "the library's own care plan comes first");
        var names = definitions.Select(d => d.Name).ToHashSet();
        Templates.SelectMany(t => t.Stages.Select(s => s.Name).Concat(t.Branches.SelectMany(b => b.StageNames)))
            .Should().OnlyContain(name => names.Contains(name));
        definitions.Should().OnlyContain(d => d.Description.Length > 0 && d.Description.Length <= 500);
    }

    [Test]
    public void Definitions_TakeTheirGatesFromTheReviewedStages()
    {
        var definitions = DemoTemplates.Definitions().ToDictionary(d => d.Name);

        definitions["Take biopsy"].NeedsConsent.Should().BeTrue();
        definitions["Take photographs"].NeedsConsent.Should().BeTrue();
        definitions["Histology result"].WaitsForResults.Should().BeTrue("a lab result waits for its results to be signed off");
        definitions["Dermoscopy assessment"].Visit.Should().Be(VisitRequirement.Required);
        definitions["Final letter to GP"].NeedsLetter.Should().Be(LetterKind.DischargeSummary);
        definitions["Refer to hospital"].NeedsLetter.Should().Be(LetterKind.OnwardReferral);
        definitions["Refer to GP"].NeedsLetter.Should().Be(LetterKind.ClinicLetter);
        (definitions["Triage"].SnomedCode, definitions["Triage"].SnomedTerm).Should().Be(("225390008", "Triage"));
        definitions["Triage"].Visit.Should().Be(VisitRequirement.None, "the shared Triage needs no visit; only the relay's pathway does");
    }

    [Test]
    public void Routes_OnlyGoForward_AndOutcomesLeadToLaterStages()
    {
        foreach (var template in Templates)
        {
            template.Links.Should().OnlyContain(l => l.FromStageOrder < l.ToStageOrder, template.Name);
            foreach (var stage in template.Stages)
            {
                if (stage.ChoiceKind is null)
                    stage.Outcomes.Should().BeEmpty();
                else
                    stage.Outcomes.Should().NotBeEmpty().And.OnlyContain(o => o.NextStageOrders.All(n => n > stage.StageOrder), $"{template.Name}: {stage.Name}");
            }
        }
    }

    [Test]
    public void Clinic_IsDermatologysTeam()
    {
        DemoDermatology.Clinic.Team.Should().Be(DemoPeople.Dermatology);
        DemoTemplates.Clinics.Select(c => c.Team).Should().Equal([DemoPeople.Dermatology, DemoPeople.EyeClinic],
            "Dermatology's stages come first in the library, so its shared definitions stay as they were");
    }

    [Test]
    public void FirstStage_AloneStopsTheClock()
    {
        foreach (var template in Templates)
            template.Stages.Where(s => s.StopsClock).Select(s => s.StageOrder).Should().Equal([1], template.Name);
    }

    [Test]
    public void SuspectedSkinCancer_HasTheLabsHistology_TheSecondLesionsRoute_AndARepeatBiopsy()
    {
        var template = Template(SuspectedSkinCancer);

        template.Stages.Take(5).Select(s => s.Name).Should().Equal("Triage", "Dermoscopy assessment", "Take biopsy", "Histology", "Histology result");
        template.Stages.Single(s => s.IsLabs).Name.Should().Be("Histology");
        template.RetentionClass.Should().Be(RetentionClass.Oncology);

        var dermoscopy = template.Stages[1];
        dermoscopy.ChoiceKind.Should().Be(StageChoiceKind.Assessment);
        dermoscopy.Outcomes.Single(o => o.Name == "Uncertain: biopsy").NextStageOrders.Should().Equal(3);

        var result = template.Stages[4];
        result.ChoiceKind.Should().Be(StageChoiceKind.LabResult);
        result.Outcomes.Select(o => o.Name).Should().NotContain("Not diagnostic", "going back to biopsy again is the repeatable route");
        template.Stages[result.Outcomes.Single(o => o.Name == "Low-risk BCC").NextStageOrders.Single() - 1].Name.Should().Be("Refer to hospital");

        template.Branches.Should().BeEquivalentTo(new[]
        {
            new DemoTemplateBranch("Biopsy another lesion", ["Take biopsy", "Histology"], [2], false),
            new DemoTemplateBranch("Repeat biopsy", ["Take biopsy", "Histology"], [4], true),
        }, o => o.WithStrictOrdering());
    }

    [Test]
    public void MoleReview_DropsTheOutcomeThatLoopsToItself()
    {
        var review = Template("Changing mole or lesion check").Stages.Single(s => s.Name == "Mole review");

        review.Outcomes.Select(o => o.Name).Should().Equal("No change", "Mole has changed");
    }

    [Test]
    public void ToursRelay_IsTriageTheLabsTestingTheResultsVisitAndAReferralBackToTheGp()
    {
        var relay = Template(DemoDermatology.SkinSampleToTheLab);
        var definitions = DemoTemplates.Definitions().ToDictionary(d => d.Name);

        relay.Stages.Select(s => (s.Name, s.IsLabs)).Should().Equal(
            ("Triage", false), ("Testing", true), ("Results appointment", false), ("Refer to GP for medication", false));
        relay.Links.Should().Equal(new PathwayTemplateLink(1, 2), new PathwayTemplateLink(2, 3), new PathwayTemplateLink(3, 4));
        definitions["Results appointment"].Visit.Should().Be(VisitRequirement.Required);
        definitions["Refer to GP for medication"].NeedsLetter.Should().Be(LetterKind.ClinicLetter);
    }

    [TestCase(null, null)]
    [TestCase("Final letter to GP", LetterKind.DischargeSummary)]
    [TestCase("Urgent referral letter to hospital", LetterKind.OnwardReferral)]
    [TestCase("Referral letter to psychology", LetterKind.ClinicLetter)]
    public void LetterKindOf_IsTheLettersKind(string? letter, LetterKind? kind) =>
        DemoTemplates.LetterKindOf(letter).Should().Be(kind);
}
