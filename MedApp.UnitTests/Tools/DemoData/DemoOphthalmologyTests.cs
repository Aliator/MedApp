using FluentAssertions;
using MedApp.DevMcp.DemoData;
using MedApp.Domain.Letters;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.UnitTests.Tools.DemoData;

/// <summary>
/// The Eye clinic's templates as the demo data makes them (<see cref="DemoOphthalmology"/>, a copy of
/// <c>docs/pathways/ophthalmology.yaml</c>), mapped onto the model by the same code as Dermatology's
/// (<see cref="DemoTemplates"/>): choice nodes with their outcomes, the lab's Microbiology, letters and the shared library.
/// </summary>
[TestFixture]
public sealed class DemoOphthalmologyTests
{
    private static readonly IReadOnlyList<DemoTemplate> Templates = DemoTemplates.Templates(DemoOphthalmology.Clinic);

    private static DemoTemplate Template(string name) => Templates.Single(t => t.Name == name);

    [Test]
    public void Templates_AreTheFileFour_InTheEyeClinic()
    {
        DemoOphthalmology.Clinic.Team.Should().Be(DemoPeople.EyeClinic);
        Templates.Select(t => (t.Name, t.RetentionClass)).Should().Equal(
            (DemoOphthalmology.CataractAssessment, RetentionClass.Standard),
            (DemoOphthalmology.GlaucomaAssessment, RetentionClass.Standard),
            (DemoOphthalmology.RedEyeAssessment, RetentionClass.Standard),
            (DemoOphthalmology.MacularAssessment, RetentionClass.Standard));
    }

    [Test]
    public void TemplateNames_AreUniqueAcrossTheClinics() =>
        DemoTemplates.Clinics.SelectMany(c => c.Templates).Select(t => t.Name).Should().OnlyHaveUniqueItems();

    [Test]
    public void Routes_OnlyGoForward_AndTheFirstStageAloneStopsTheClock()
    {
        foreach (var template in Templates)
        {
            template.Links.Should().OnlyContain(l => l.FromStageOrder < l.ToStageOrder, template.Name);
            template.Stages.Where(s => s.StopsClock).Select(s => s.StageOrder).Should().Equal([1], template.Name);
            template.Stages.Where(s => s.ChoiceKind is not null)
                .Should().OnlyContain(s => s.Outcomes.Count > 0 && s.Outcomes.All(o => o.NextStageOrders.All(n => n > s.StageOrder)), template.Name);
            template.Branches.Should().BeEmpty(template.Name);
        }
    }

    [Test]
    public void Cataract_AssessmentChoosesBiometry_ThenTheReferralForSurgery()
    {
        var template = Template(DemoOphthalmology.CataractAssessment);

        template.Stages.Take(4).Select(s => s.Name).Should().Equal("Triage", "Cataract assessment", "Biometry", "Refer for cataract surgery");
        var assessment = template.Stages[1];
        assessment.ChoiceKind.Should().Be(StageChoiceKind.Assessment);
        assessment.Outcomes.Select(o => o.Name).Should().Equal("Wants surgery and suitable", "Not affecting daily life", "Other eye problem found");
        assessment.Outcomes[0].NextStageOrders.Should().Equal(3);
        template.Links.Should().Contain(new PathwayTemplateLink(3, 4));
    }

    [Test]
    public void RedEye_HasTheLabsMicrobiology_AndItsResultAsALabResult()
    {
        var template = Template(DemoOphthalmology.RedEyeAssessment);

        template.Stages.Take(5).Select(s => (s.Name, s.IsLabs)).Should().Equal(
            ("Triage", false), ("Eye examination", false), ("Take eye swab", false), ("Microbiology", true), ("Microbiology result", false));
        template.Stages[1].Outcomes.Single(o => o.Name == "Swab needed").NextStageOrders.Should().Equal(3);
        template.Stages[4].ChoiceKind.Should().Be(StageChoiceKind.LabResult);
        template.Stages[4].Outcomes.Select(o => o.Name).Should().Equal("No growth", "Bacterial infection", "Unusual organism");
    }

    [Test]
    public void Glaucoma_BothFirstOutcomesLeadToTheReview()
    {
        var template = Template(DemoOphthalmology.GlaucomaAssessment);
        int OrderOf(string name) => template.Stages.Single(s => s.Name == name).StageOrder;

        template.Links.Should().Contain(new PathwayTemplateLink(OrderOf("Start eye drops"), OrderOf("Glaucoma review")))
            .And.Contain(new PathwayTemplateLink(OrderOf("Agree monitoring plan"), OrderOf("Glaucoma review")));
        template.Stages.Single(s => s.Name == "Glaucoma review").Outcomes.Select(o => o.Name).Should().Equal("Stable", "Not controlled");
    }

    [Test]
    public void Definitions_ShareDermatologysByName_AndAddTheEyeClinicsOwn()
    {
        var definitions = DemoTemplates.Definitions().ToDictionary(d => d.Name);
        var dermatologyOnly = DemoTemplates.Definitions([DemoDermatology.Clinic]).ToDictionary(d => d.Name);

        definitions["Triage"].Should().Be(dermatologyOnly["Triage"]);
        definitions["Microbiology"].Should().Be(dermatologyOnly["Microbiology"], "the lab's Microbiology is one stage definition for both clinics");
        definitions["Final letter to GP"].NeedsLetter.Should().Be(LetterKind.DischargeSummary);
        definitions["Microbiology result"].WaitsForResults.Should().BeTrue();
        definitions["Biometry"].Visit.Should().Be(VisitRequirement.Required);
        definitions["Refer for cataract surgery"].NeedsLetter.Should().Be(LetterKind.OnwardReferral);
        definitions["Urgent hospital eye referral"].NeedsLetter.Should().Be(LetterKind.OnwardReferral);
        definitions["Back to optometrist monitoring"].NeedsLetter.Should().Be(LetterKind.DischargeSummary);
        definitions["Refer to hospital eye service"].Description.Should().Be("Refer to the hospital eye service for specialist care or treatment.",
            "a name several of its templates share takes the clinic's shared description, not one template's notes");
        (definitions["Refer for cataract surgery"].SnomedCode, definitions["Refer for cataract surgery"].SnomedTerm).Should().Be(("306206005", "Referral to service"));
        definitions.Values.Should().OnlyContain(d => d.Description.Length > 0 && d.Description.Length <= 500);
        Templates.SelectMany(t => t.Stages).Select(s => s.Name).Should().OnlyContain(name => definitions.ContainsKey(name));
    }
}
