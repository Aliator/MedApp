using FluentAssertions;
using MedApp.Application.Pathways.PatientPathwayStages.Templates;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Pathways.PatientPathwayStages.Templates;

/// <summary><see cref="TemplateGraphRules"/> (the light demo's <c>templateGraphProblems</c>) and <see cref="TemplateShape"/>.</summary>
[TestFixture]
public sealed class TemplateGraphRulesTests
{
    private static (bool, StageChoiceKind?, IReadOnlyList<PathwayTemplateOutcome>) Plain(bool optional = false) => (optional, null, []);

    private static (bool, StageChoiceKind?, IReadOnlyList<PathwayTemplateOutcome>) Choice(params PathwayTemplateOutcome[] outcomes) =>
        (false, StageChoiceKind.Assessment, outcomes);

    private static PathwayTemplateLink L(int from, int to) => new(from, to);

    [Test]
    public void Problems_AChainAndAForkWithItsJoin_HaveNone()
    {
        TemplateGraphRules.Problems([Plain(), Plain(), Plain()], [L(1, 2), L(2, 3)]).Should().BeEmpty();
        TemplateGraphRules.Problems([Plain(), Plain(), Plain(), Plain()], [L(1, 2), L(1, 3), L(2, 4), L(3, 4)]).Should().BeEmpty();
    }

    [Test]
    public void Problems_AChoiceNodeWithTwoNamedOutcomes_OneEndingTheRoute_HasNone()
    {
        TemplateGraphRules.Problems(
                [Plain(), Choice(new PathwayTemplateOutcome("Refer", []), new PathwayTemplateOutcome("Treat", [3])), Plain()],
                [L(1, 2), L(2, 3)])
            .Should().BeEmpty();
    }

    [Test]
    public void Problems_EachRuleBroken_SaysTheDemosWords_ForTheStage()
    {
        TemplateGraphRules.Problems([Plain(), Plain()], [L(2, 1)]).Should().Contain(0, TemplateGraphRules.BackwardLinkMessage);
        TemplateGraphRules.Problems([Plain(), Plain(), Plain()], [L(1, 2)]).Should().Contain(2, TemplateGraphRules.NoPredecessorMessage);
        TemplateGraphRules.Problems([Plain(), Plain(optional: true)], [L(1, 2)]).Should().Contain(1, TemplateGraphRules.LastStageMessage);
        TemplateGraphRules.Problems([Choice(new PathwayTemplateOutcome("A", [2]), new PathwayTemplateOutcome("B", [2])), Plain()], [L(1, 2)]).Should().BeEmpty();
        TemplateGraphRules.Problems([Choice(new PathwayTemplateOutcome("A", [2])), Plain()], [L(1, 2)]).Should().Contain(0, TemplateGraphRules.NoOutcomesMessage);
        TemplateGraphRules.Problems([Choice(new PathwayTemplateOutcome("A", [2]), new PathwayTemplateOutcome("a", [2])), Plain()], [L(1, 2)])
            .Should().Contain(0, TemplateGraphRules.SameOutcomeNameMessage);
        TemplateGraphRules.Problems([Choice(new PathwayTemplateOutcome("A", [3]), new PathwayTemplateOutcome("B", [2])), Plain(), Plain()], [L(1, 2), L(2, 3)])
            .Should().Contain(0, TemplateGraphRules.OutcomeNotAfterMessage);
        TemplateGraphRules.Problems([Choice(new PathwayTemplateOutcome("A", []), new PathwayTemplateOutcome("B", [])), Plain(), Plain()], [L(1, 2), L(2, 3)])
            .Should().Contain(1, TemplateGraphRules.UnnamedOutcomeMessage);
        TemplateGraphRules.Problems([Plain(), Choice(new PathwayTemplateOutcome("A", []), new PathwayTemplateOutcome("B", [])), Plain()], [L(1, 2), L(1, 3)])
            .Should().Contain(1, TemplateGraphRules.DeadEndChoiceMessage);
        TemplateGraphRules.Problems([(true, StageChoiceKind.LabResult, [new PathwayTemplateOutcome("A", [2]), new PathwayTemplateOutcome("B", [2])]), Plain()], [L(1, 2)])
            .Should().Contain(0, TemplateGraphRules.ChoiceOptionalMessage);
    }

    [Test]
    public void Links_ProblemsSent_AreA400OnLinksNamingTheFirstStage()
    {
        var maps = TemplateShape.ToMaps(Guid.NewGuid(), TeamId, [ConsultationDefinitionId, PathwayDefinitionId, PathwayDefinitionId], null, null);

        TemplateShape.Links(maps, [L(1, 2)], null, out var problem).Should().BeNull();

        problem.Should().Be(("Links", $"Stage 3: {TemplateGraphRules.NoPredecessorMessage}"));
    }

    [Test]
    public void Links_AListWithoutLinks_IsAChain_ButAChoiceNodeOrALastOptionalStageIsRefused()
    {
        var maps = TemplateShape.ToMaps(Guid.NewGuid(), TeamId, [ConsultationDefinitionId, PathwayDefinitionId], null, null);
        TemplateShape.Links(maps, null, null, out _).Should().Equal(L(1, 2));

        maps[1].IsOptional = true;
        TemplateShape.Links(maps, null, null, out var lastOptional);
        lastOptional.Should().Be(("StageOptions", TemplateShape.LastOptionalMessage));

        maps[1].IsOptional = false;
        maps[0].ChoiceKind = StageChoiceKind.Assessment;
        TemplateShape.Links(maps, null, null, out var choice);
        choice.Should().Be(("Links", TemplateShape.ChoiceNeedsLinksMessage));
    }

    [Test]
    public void WhyOptionsInvalid_AnOwnerOtherThanTheTemplatesTeamOrTheLab_OrAnUnknownChoiceKind()
    {
        var lab = Guid.NewGuid();

        TemplateShape.WhyOptionsInvalid([new(OwnerTeamId: lab), new(OwnerTeamId: TeamId), new(ChoiceKind: "LabResult")], TeamId, lab).Should().BeNull();
        TemplateShape.WhyOptionsInvalid([new(OwnerTeamId: Guid.NewGuid())], TeamId, lab).Should().Be(("StageOptions", TemplateShape.OwnerMessage));
        TemplateShape.WhyOptionsInvalid([new(ChoiceKind: "Surgery")], TeamId, lab).Should().Be(("StageOptions", TemplateShape.ChoiceKindMessage));
    }

    [Test]
    public void Branches_WellFormed_AreKeptWithTheirIds_AndEachRuleIsA400()
    {
        var definition = Guid.NewGuid();
        var known = new HashSet<Guid> { definition };
        var existing = new PathwayTemplateBranch(Guid.NewGuid(), "Repeat", [definition], [1], false);

        var kept = TemplateShape.Branches(
            [new TemplateBranchInput(existing.Id, " Repeat ", [definition], [2, 1, 1], true)], [existing], 3, known, out var none);

        none.Should().BeNull();
        kept!.Single().Should().BeEquivalentTo(new PathwayTemplateBranch(existing.Id, "Repeat", [definition], [1, 2], true));

        Problem(new TemplateBranchInput(null, " ", [definition], [], false)).Should().Be(TemplateShape.BranchNameMessage);
        Problem(new TemplateBranchInput(null, new string('x', 51), [definition], [], false)).Should().Be(TemplateShape.BranchNameTooLongMessage);
        Problem(new TemplateBranchInput(null, "Repeat", [], [], false)).Should().Be(TemplateShape.BranchNeedsStagesMessage("Repeat"));
        Problem(new TemplateBranchInput(null, "Repeat", [Guid.NewGuid()], [], false)).Should().Be(TemplateShape.BranchStageMissingMessage("Repeat"));
        Problem(new TemplateBranchInput(null, "Repeat", [definition], [3], false)).Should().Be(TemplateShape.BranchAttachMessage("Repeat"));
        TemplateShape.Branches(
            [new TemplateBranchInput(null, "A", [definition], [], false), new TemplateBranchInput(null, "a", [definition], [], false)],
            [], 3, known, out var twice);
        twice.Should().Be(("AddableBranches", TemplateShape.BranchNamesDifferMessage));

        string? Problem(TemplateBranchInput branch)
        {
            TemplateShape.Branches([branch], [], 3, known, out var problem);
            return problem?.Message;
        }
    }
}
