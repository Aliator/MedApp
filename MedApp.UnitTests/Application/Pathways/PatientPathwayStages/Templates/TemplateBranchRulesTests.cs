using FluentAssertions;
using MedApp.Application.Pathways.PatientPathwayStages.Templates;

namespace MedApp.UnitTests.Application.Pathways.PatientPathwayStages.Templates;

[TestFixture]
public sealed class TemplateBranchRulesTests
{
    private static readonly TemplateStageOptions Plain = TemplateStageOptions.Plain;
    private static readonly TemplateStageOptions Point = new(ChoosesBranch: true);
    private static readonly TemplateStageOptions Optional = new(IsOptional: true);

    private static TemplateStageOptions In(string branch, bool optional = false) => new(optional, false, branch);

    [Test]
    public void WhyInvalid_PlainStages_IsNull()
    {
        TemplateBranchRules.WhyInvalid([Plain, Plain, Plain]).Should().BeNull();
        TemplateBranchRules.WhyInvalid([]).Should().BeNull();
    }

    [Test]
    public void WhyInvalid_WellFormedBranches_IsNull()
    {
        TemplateBranchRules.WhyInvalid(
                [Plain, Point, In("Benign"), In("Treatment"), In("treatment", optional: true), Optional, Plain])
            .Should().BeNull();
    }

    [Test]
    public void WhyInvalid_TwoBranchPointsEachWithTwoBranches_IsNull()
    {
        TemplateBranchRules.WhyInvalid([Point, In("A"), In("B"), Point, In("C"), In("D"), Plain]).Should().BeNull();
    }

    [TestCase(true, false, null)]
    [TestCase(false, true, null)]
    [TestCase(false, false, "Benign")]
    public void WhyInvalid_LastStageThatMightNotRun_IsRefused(bool optional, bool point, string? branch)
    {
        TemplateBranchRules.WhyInvalid([Point, In("Benign"), In("Treatment"), new TemplateStageOptions(optional, point, branch)])
            .Should().Be(TemplateBranchRules.LastStageMessage);
    }

    [Test]
    public void WhyInvalid_BranchPointWithOneBranch_IsRefused()
    {
        TemplateBranchRules.WhyInvalid([Point, In("Benign"), In("BENIGN"), Plain])
            .Should().Be(TemplateBranchRules.TooFewBranchesMessage);
    }

    [Test]
    public void WhyInvalid_BranchesAfterTheNextBranchPointDontCount_IsRefused()
    {
        TemplateBranchRules.WhyInvalid([Point, In("A"), Point, In("B"), In("C"), Plain])
            .Should().Be(TemplateBranchRules.TooFewBranchesMessage);
    }

    [Test]
    public void WhyInvalid_BranchBeforeAnyBranchPoint_IsRefused()
    {
        TemplateBranchRules.WhyInvalid([In("A"), Point, In("A"), In("B"), Plain])
            .Should().Be(TemplateBranchRules.BranchWithoutPointMessage);
    }

    [TestCase(true, null)]
    [TestCase(false, "A")]
    public void WhyInvalid_BranchPointOptionalOrInABranch_IsRefused(bool optional, string? branch)
    {
        TemplateBranchRules.WhyInvalid([Point, In("A"), In("B"), new TemplateStageOptions(optional, true, branch), In("C"), In("D"), Plain])
            .Should().Be(TemplateBranchRules.BranchPointOptionsMessage);
    }

    [Test]
    public void WhyInvalid_BranchNameTooLong_IsRefused()
    {
        TemplateBranchRules.WhyInvalid([Point, In(new string('x', 51)), In("B"), Plain]).Should().Contain("50 characters");
    }

    [Test]
    public void BranchName_BlankOrPadded_IsNullOrTrimmed()
    {
        new TemplateStageOptions(Branch: "  ").BranchName.Should().BeNull();
        new TemplateStageOptions(Branch: " Benign ").BranchName.Should().Be("Benign");
    }

    [Test]
    public void OrPlain_NoOptions_GivesAPlainStageForEach()
    {
        TemplateBranchRules.OrPlain(null, 3).Should().Equal(Plain, Plain, Plain);
    }
}
