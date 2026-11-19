using FluentAssertions;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Pathways.Common;

/// <summary>
/// Optional stages, skipped stages, tree pathways (MC8: links, forks, joins) and choice nodes (MC10b: pathways grow) in
/// <see cref="PathwayWorkflow"/>, <see cref="PathwayGraph"/>, <see cref="PathwayGrowth"/> and <see cref="PathwayChange"/>.
/// </summary>
[TestFixture]
public sealed class PathwayWorkflowBranchTests
{
    [Test]
    public void CurrentStage_SkippedStagesFirst_IsTheFirstStillToDo()
    {
        var pathway = CreatePathway(Status.InProgress);
        Skip(pathway.StageAt(1));

        PathwayWorkflow.CurrentStage(pathway).Should().Be(pathway.StageAt(2));
    }

    [Test]
    public void AllStagesFinished_FinishedOrSkipped_IsTrue()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 2);
        Skip(pathway.StageAt(3));

        PathwayWorkflow.AllStagesFinished(pathway).Should().BeTrue();
        PathwayWorkflow.CurrentStage(pathway).Should().BeNull();
    }

    [Test]
    public void StageState_Skipped_IsSkipped()
    {
        var stage = CreatePathway().StageAt(1);
        Skip(stage);

        PathwayWorkflow.StageState(stage).Should().Be(PathwayWorkflow.StageSkipped);
    }

    [TestCase(Status.InProgress, true)]
    [TestCase(Status.InReview, true)]
    [TestCase(Status.Completed, true)]
    [TestCase(Status.Blocked, false)]
    [TestCase(Status.Cancelled, false)]
    [TestCase(Status.Archived, false)]
    public void CanReinstateStage_SkippedStage_AsUnlockingAllows(Status status, bool expected)
    {
        var pathway = CreatePathway(status);
        Skip(pathway.StageAt(2));

        PathwayWorkflow.CanReinstateStage(pathway, pathway.StageAt(2)).Should().Be(expected);
        PathwayWorkflow.CanReinstateStage(pathway, pathway.StageAt(1)).Should().BeFalse();
    }

    [Test]
    public void WhyStageCantBeEdited_SkippedStage_SaysItCanBeReinstated()
    {
        var pathway = CreatePathway(Status.InProgress);
        Skip(pathway.StageAt(1));

        PathwayWorkflow.WhyStageCantBeEdited(pathway, pathway.StageAt(1)).Should().Contain("was skipped").And.Contain("reinstate");
    }

    [TestCase(VisitRequirement.Required, false, false)]
    [TestCase(VisitRequirement.Required, true, true)]
    [TestCase(VisitRequirement.Suggested, false, true)]
    [TestCase(VisitRequirement.None, false, true)]
    public void CanSkipStage_RequiredVisit_NeedsFinishWithoutVisit(VisitRequirement requirement, bool canFinishWithoutVisit, bool expected)
    {
        var pathway = CreatePathway(Status.InProgress);
        pathway.StageAt(1).IsOptional = true;
        pathway.StageAt(1).VisitRequirement = requirement;

        PathwayWorkflow.CanSkipStage(pathway, pathway.StageAt(1), canFinishWithoutVisit).Should().Be(expected);
    }

    [Test]
    public void ReinstateStage_ClearsTheSkipAndKeepsHowItWasSkippedInTheAudit()
    {
        var pathway = CreatePathway(Status.Completed, finishedStages: 2);
        Skip(pathway.StageAt(3), StageSkipReason.Other, SkipDetail);
        var change = new PathwayChange(pathway, Admin, Now);

        change.ReinstateStage(pathway.StageAt(3), "Synthetic: needed after all.");

        pathway.StageAt(3).Should().Match<PatientPathwayStage>(s =>
            s.SkippedAtUtc == null && s.SkippedByUserId == null && s.SkipReason == null && s.SkipDetail == null);
        pathway.Status.Should().Be(Status.InProgress);
        change.AuditEntries.Select(e => e.Action).Should().Equal(
            PatientPathwayAuditAction.StageReinstated, PatientPathwayAuditAction.StatusChanged);
        change.AuditEntries[0].Details.Should().Contain("\"skipReason\":\"Other\"");
    }

    // Tree pathways (MC8) --------------------------------------------------------------------------------------------

    /// <summary>A fork: 1, then 2 and 3 side by side, joined at 4.</summary>
    private static PatientPathway Fork()
    {
        var pathway = CreatePathway(Status.InProgress);
        var id = pathway.Id;
        pathway.Stages = [.. Enumerable.Range(1, 4).Select(o => CreateStage(id, o, PathwayDefinitionId, $"Stage {o}"))];
        Link(pathway, 1, 2);
        Link(pathway, 1, 3);
        Link(pathway, 2, 4);
        Link(pathway, 3, 4);
        return pathway;
    }

    private static void Link(PatientPathway pathway, int from, int to) =>
        pathway.Links.Add(new PatientPathwayStageLink
        {
            PatientPathwayId = pathway.Id, FromStageId = pathway.StageAt(from).Id, ToStageId = pathway.StageAt(to).Id
        });

    [Test]
    public void CurrentStages_AFork_RunsItsRoutesSideBySide()
    {
        var pathway = Fork();
        Finish(pathway.StageAt(1));

        PathwayWorkflow.CurrentStages(pathway).Should().Equal(pathway.StageAt(2), pathway.StageAt(3));
        PathwayWorkflow.CurrentStage(pathway).Should().Be(pathway.StageAt(2));
    }

    [Test]
    public void CurrentStages_AJoin_WaitsForEachRoute()
    {
        var pathway = Fork();
        Finish(pathway.StageAt(1));
        Finish(pathway.StageAt(2));

        PathwayWorkflow.CurrentStages(pathway).Should().Equal(pathway.StageAt(3));

        Skip(pathway.StageAt(3));

        PathwayWorkflow.CurrentStages(pathway).Should().Equal(pathway.StageAt(4));
    }

    [Test]
    public void CurrentStages_AStageReinstatedBeforeOthers_HoldsUpTheStagesAfterIt()
    {
        var pathway = CreatePathway(Status.InProgress);
        Skip(pathway.StageAt(1));
        Finish(pathway.StageAt(2));
        PathwayWorkflow.CurrentStages(pathway).Should().Equal(pathway.StageAt(3));

        pathway.StageAt(1).SkippedAtUtc = null;

        PathwayWorkflow.CurrentStages(pathway).Should().Equal(pathway.StageAt(1));
    }

    [Test]
    public void Predecessors_AJoin_HasEachRoutesLastStage_AndAChainItsStageBefore()
    {
        var fork = Fork();
        var chain = CreatePathway();

        PathwayGraph.Predecessors(fork, fork.StageAt(4)).Should().Equal(fork.StageAt(2), fork.StageAt(3));
        PathwayGraph.Predecessors(chain, chain.StageAt(3)).Should().Equal(chain.StageAt(2));
        PathwayGraph.Predecessors(chain, chain.StageAt(1)).Should().BeEmpty();
    }

    [Test]
    public void WhyStageCantBeEdited_AStageWhoseRouteIsntDone_NamesTheStageToFinishFirst()
    {
        var pathway = Fork();
        Finish(pathway.StageAt(1));
        Finish(pathway.StageAt(2));

        PathwayWorkflow.WhyStageCantBeEdited(pathway, pathway.StageAt(4)).Should().Contain("'Stage 3'");
        PathwayWorkflow.WhyStageCantBeEdited(pathway, pathway.StageAt(3)).Should().BeNull();
    }

    // Choice nodes (MC10b) -------------------------------------------------------------------------------------------

    [Test]
    public void CreateBranchingPathway_StartsWithTheStagesUpToItsFirstChoiceNode()
    {
        var pathway = CreateBranchingPathway();

        pathway.Stages.Select(s => s.Name).Should().Equal(ResultsStage);
        pathway.StageAt(1).ChoiceKind.Should().Be(StageChoiceKind.Assessment);
    }

    [Test]
    public void GrownStageOrders_AnOutcomeRecorded_AddsItsRouteAndTheJoin_NeverTheOther()
    {
        var plan = BranchingPlan();

        PathwayGrowth.GrownStageOrders(plan.Stages, plan.Links, new Dictionary<int, string>()).Should().Equal(1);
        PathwayGrowth.GrownStageOrders(plan.Stages, plan.Links, new Dictionary<int, string> { [1] = TreatmentBranch })
            .Should().Equal(1, 3, 4, 5);
        PathwayGrowth.GrownStageOrders(plan.Stages, plan.Links, new Dictionary<int, string> { [1] = "benign" })
            .Should().Equal(1, 2, 5);
    }

    [Test]
    public void GrownStageOrders_AJoinAfterAForkWithAChoiceNode_WaitsUntilTheChoiceIsRecorded()
    {
        // 1 forks to 2 (a choice node, whose outcomes lead to 3 or end the route) and 4; 3 and 4 join at 5.
        var stages = new[]
        {
            Planned(1, PathwayDefinitionId, "One"),
            Planned(2, PathwayDefinitionId, "Two", StageChoiceKind.LabResult,
                [new PathwayTemplateOutcome("More", [3]), new PathwayTemplateOutcome("Done", [])]),
            Planned(3, PathwayDefinitionId, "Three"),
            Planned(4, PathwayDefinitionId, "Four"),
            Planned(5, PathwayDefinitionId, "Five"),
        };
        var links = new[]
        {
            new PathwayTemplateLink(1, 2), new PathwayTemplateLink(1, 4), new PathwayTemplateLink(2, 3),
            new PathwayTemplateLink(3, 5), new PathwayTemplateLink(4, 5)
        };

        PathwayGrowth.GrownStageOrders(stages, links, new Dictionary<int, string>()).Should().Equal(1, 2, 4);
        PathwayGrowth.GrownStageOrders(stages, links, new Dictionary<int, string> { [2] = "More" }).Should().Equal(1, 2, 3, 4, 5);
        PathwayGrowth.GrownStageOrders(stages, links, new Dictionary<int, string> { [2] = "Done" }).Should().Equal(1, 2, 4, 5);
    }

    [Test]
    public void Outcomes_AChoiceNode_ListsEachWithTheStagesItWouldAdd_NonePreSelected()
    {
        var pathway = CreateBranchingPathway();

        PathwayGrowth.Outcomes(pathway, pathway.StageAt(1)).Select(o => (o.Name, string.Join("|", o.StageNames))).Should().Equal(
            (BenignBranch, $"{BenignStage}|{DischargeStage}"),
            (TreatmentBranch, $"{TreatmentStage}|{FollowUpStage}|{DischargeStage}"));
        pathway.StageAt(1).Outcome.Should().BeNull();
    }

    [Test]
    public void WhyOutcomeInvalid_AChoiceNodeWithoutOne_NamesTheOutcomes_AndAPlainStageRefusesOne()
    {
        var pathway = CreateBranchingPathway();
        var plain = CreatePathway(Status.InProgress);

        PathwayWorkflow.WhyOutcomeInvalid(pathway, pathway.StageAt(1), null).Should().Be($"Record the outcome: {BenignBranch}, {TreatmentBranch}.");
        PathwayWorkflow.WhyOutcomeInvalid(pathway, pathway.StageAt(1), "Surgery").Should().StartWith("Record the outcome");
        PathwayWorkflow.WhyOutcomeInvalid(pathway, pathway.StageAt(1), " treatment ").Should().BeNull();
        PathwayWorkflow.WhyOutcomeInvalid(plain, plain.StageAt(1), TreatmentBranch).Should().Contain("recorded already");
        PathwayWorkflow.WhyOutcomeInvalid(plain, plain.StageAt(1), null).Should().BeNull();
    }

    [Test]
    public void FinishStage_AnOutcome_RecordsItGrowsThePathwayAndAuditsTheStagesAdded()
    {
        var pathway = CreateBranchingPathway();
        var change = new PathwayChange(pathway, Staff, Now);

        var added = change.FinishStage(pathway.StageAt(1), outcome: "treatment");

        pathway.StageAt(1).Outcome.Should().Be(TreatmentBranch);
        added.Select(s => s.Name).Should().Equal(TreatmentStage, FollowUpStage, DischargeStage);
        pathway.Stages.OrderBy(s => s.StageOrder).Select(s => s.Name).Should().Equal(ResultsStage, TreatmentStage, FollowUpStage, DischargeStage);
        change.NewStages.Should().BeEquivalentTo(added);
        change.NewLinks.Should().HaveCount(3);
        PathwayWorkflow.CurrentStages(pathway).Should().Equal(pathway.TemplateStageAt(3)!);
        var chosen = change.AuditEntries.Single(e => e.Action == PatientPathwayAuditAction.BranchChosen);
        chosen.Details.Should().Contain($"\"outcome\":\"{TreatmentBranch}\"").And.Contain(added[0].Id.ToString());
        change.AuditEntries.Select(e => e.Action).Should().ContainInOrder(
            PatientPathwayAuditAction.StageFinished, PatientPathwayAuditAction.BranchChosen);
    }

    [Test]
    public void FinishStage_TheLastStageOnEveryRoute_PutsThePathwayInReview_AndAChoiceNodeIsNeverLast()
    {
        var pathway = CreateBranchingPathway(outcome: BenignBranch);
        var change = new PathwayChange(pathway, Staff, Now);

        StageGates.IsLastToFinish(pathway, pathway.TemplateStageAt(2)!).Should().BeFalse();
        change.FinishStage(pathway.TemplateStageAt(2)!);
        StageGates.IsLastToFinish(pathway, pathway.TemplateStageAt(5)!).Should().BeTrue();
        change.FinishStage(pathway.TemplateStageAt(5)!);

        pathway.Status.Should().Be(Status.InReview);
        var fresh = CreateBranchingPathway();
        StageGates.IsLastToFinish(fresh, fresh.StageAt(1)).Should().BeFalse();
    }

    [Test]
    public void FromTemplate_KeepsTheTemplatesTreeAsItsPlan_AndStartsWithTheStagesUpToTheFirstChoiceNode()
    {
        var template = CreateTemplate(TemplateName, false,
            CreateDefinition(ResultsStage), CreateDefinition(BenignStage), CreateDefinition(TreatmentStage), CreateDefinition(DischargeStage));
        var maps = template.Maps.OrderBy(m => m.StageOrder).ToList();
        maps[0].ChoiceKind = StageChoiceKind.Assessment;
        maps[0].Outcomes = [new PathwayTemplateOutcome(BenignBranch, [2]), new PathwayTemplateOutcome(TreatmentBranch, [3])];
        maps[2].IsOptional = true;
        template.Links = [new(1, 2), new(1, 3), new(2, 4), new(3, 4)];

        var pathway = PathwayCreation.FromTemplate(template, Guid.NewGuid(), null, Today.AddDays(7), PatientPathwayPriority.Medium, Now);

        pathway.Stages.Select(s => (s.Name, s.ChoiceKind, s.TemplateStageOrder)).Should().Equal((ResultsStage, (StageChoiceKind?)StageChoiceKind.Assessment, (int?)1));
        pathway.Plan!.Stages.Select(s => s.IsOptional).Should().Equal(false, false, true, false);
        pathway.Plan.Links.Should().HaveCount(4);
    }

    [Test]
    public void FromTemplate_AListWithoutLinks_IsAChainWithEveryStage()
    {
        var template = CreateTemplate(TemplateName, false, CreateDefinition(ConsultationStage), CreateDefinition(PathwayStage), CreateDefinition(DischargeStage));

        var pathway = PathwayCreation.FromTemplate(template, Guid.NewGuid(), null, Today.AddDays(7), PatientPathwayPriority.Medium, Now);

        pathway.Stages.OrderBy(s => s.StageOrder).Select(s => s.Name).Should().Equal(ConsultationStage, PathwayStage, DischargeStage);
        pathway.Links.Should().HaveCount(2);
        PathwayWorkflow.CurrentStages(pathway).Should().Equal(pathway.StageAt(1));
    }

    [Test]
    public void FromTemplate_CopiesTheRetentionClass()
    {
        var template = CreateTemplate(TemplateName, false, CreateDefinition(ResultsStage));
        template.RetentionClass = RetentionClass.Oncology;

        var pathway = PathwayCreation.FromTemplate(template, Guid.NewGuid(), null, Today.AddDays(7), PatientPathwayPriority.Medium, Now);

        pathway.RetentionClass.Should().Be(RetentionClass.Oncology);
    }
}
