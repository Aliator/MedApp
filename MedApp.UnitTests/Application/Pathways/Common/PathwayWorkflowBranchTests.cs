using FluentAssertions;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Pathways.Common;

/// <summary>Optional stages, branch points and skipped stages in <see cref="PathwayWorkflow"/> and <see cref="PathwayChange"/>.</summary>
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

    [Test]
    public void ExistingPathway_NoOptionsOrBranches_BehavesAsBefore()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 1);

        PathwayWorkflow.CurrentStage(pathway).Should().Be(pathway.StageAt(2));
        pathway.Stages.Should().AllSatisfy(s =>
        {
            PathwayWorkflow.Branches(pathway, s).Should().BeEmpty();
            PathwayWorkflow.CanSkipStage(pathway, s, canOverrideStages: true).Should().BeFalse();
            PathwayWorkflow.WhyBranchChoiceInvalid(pathway, s, null).Should().BeNull();
        });
    }

    [Test]
    public void Branches_BranchPoint_ListsEachBranchInTheTemplatesOrderWithItsStages()
    {
        var pathway = CreateBranchingPathway();

        var branches = PathwayWorkflow.Branches(pathway, pathway.StageAt(1));

        branches.Select(b => b.Name).Should().Equal(BenignBranch, TreatmentBranch);
        branches[0].Stages.Should().Equal(pathway.StageAt(2));
        branches[1].Stages.Should().Equal(pathway.StageAt(3), pathway.StageAt(4));
    }

    [Test]
    public void Branches_NamesDifferingOnlyInCase_AreOneBranchNamedAsFirstWritten()
    {
        var pathway = CreateBranchingPathway();
        pathway.StageAt(4).Branch = " treatment ";

        PathwayWorkflow.Branches(pathway, pathway.StageAt(1)).Select(b => b.Name).Should().Equal(BenignBranch, TreatmentBranch);
    }

    [Test]
    public void Branches_StopAtTheNextBranchPoint()
    {
        var pathway = CreateBranchingPathway();
        pathway.StageAt(4).ChoosesBranch = true;
        pathway.StageAt(4).Branch = null;
        pathway.StageAt(5).Branch = "Later";

        PathwayWorkflow.Branches(pathway, pathway.StageAt(1)).Select(b => b.Name).Should().Equal(BenignBranch, TreatmentBranch);
        PathwayWorkflow.Branches(pathway, pathway.StageAt(1))[1].Stages.Should().Equal(pathway.StageAt(3));
    }

    [Test]
    public void Branches_NotABranchPoint_IsEmpty()
    {
        var pathway = CreateBranchingPathway();

        PathwayWorkflow.Branches(pathway, pathway.StageAt(2)).Should().BeEmpty();
    }

    [Test]
    public void StagesSkippedByBranch_LeavesTheChosenBranchAndStagesThatAlwaysRun()
    {
        var pathway = CreateBranchingPathway();

        PathwayWorkflow.StagesSkippedByBranch(pathway, pathway.StageAt(1), BenignBranch)
            .Should().Equal(pathway.StageAt(3), pathway.StageAt(4));
        PathwayWorkflow.StagesSkippedByBranch(pathway, pathway.StageAt(1), null).Should().BeEmpty();
    }

    [TestCase(null, "Choose which branch*")]
    [TestCase("", "Choose which branch*")]
    [TestCase("Surgery", "'Surgery' isn't one of the branches*")]
    public void WhyBranchChoiceInvalid_BranchPointFirstFinish_NeedsOneOfItsBranches(string? branch, string expected)
    {
        var pathway = CreateBranchingPathway();

        PathwayWorkflow.WhyBranchChoiceInvalid(pathway, pathway.StageAt(1), branch).Should().Match(expected);
        PathwayWorkflow.WhyBranchChoiceInvalid(pathway, pathway.StageAt(1), "TREATMENT").Should().BeNull();
    }

    [Test]
    public void WhySkipNotAllowed_OptionalCurrentStage_IsNull()
    {
        var pathway = CreateBranchingPathway(finishedStages: 1);
        Skip(pathway.StageAt(2), StageSkipReason.BranchNotChosen);
        Finish(pathway.StageAt(3));

        PathwayWorkflow.WhySkipNotAllowed(pathway, pathway.StageAt(4), false).Should().BeNull();
    }

    [Test]
    public void WhySkipNotAllowed_AlreadySkippedOrFinished_SaysSo()
    {
        var pathway = CreateBranchingPathway(finishedStages: 1);
        pathway.StageAt(1).IsOptional = true;
        pathway.StageAt(2).IsOptional = true;
        Skip(pathway.StageAt(2));

        PathwayWorkflow.WhySkipNotAllowed(pathway, pathway.StageAt(1), false).Should().Contain("is finished");
        PathwayWorkflow.WhySkipNotAllowed(pathway, pathway.StageAt(2), false).Should().Contain("already been skipped");
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

    [Test]
    public void FinishStage_BranchChosen_SkipsTheOthersBeforeTheStatusIsWorkedOut()
    {
        var pathway = CreateBranchingPathway(finishedStages: 0);
        // Only the branch point and one stage of each branch: choosing one leaves the other skipped.
        pathway.Stages.RemoveAll(s => s.StageOrder >= 4);
        var change = new PathwayChange(pathway, Staff, Now);

        var skipped = change.FinishStage(pathway.StageAt(1), branch: BenignBranch);

        skipped.Should().Equal(pathway.StageAt(3));
        pathway.StageAt(3).SkipReason.Should().Be(StageSkipReason.BranchNotChosen);
        pathway.StageAt(3).SkippedByUserId.Should().Be(Staff.UserId);
        pathway.StageAt(3).SkipDetail.Should().BeNull();
        PathwayWorkflow.CurrentStage(pathway).Should().Be(pathway.StageAt(2));
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

    [Test]
    public void FromTemplate_CopiesEachStagesOptionsOntoThePathway()
    {
        var template = CreateTemplate(TemplateName, false, CreateDefinition(ResultsStage), CreateDefinition(BenignStage), CreateDefinition(TreatmentStage), CreateDefinition(DischargeStage));
        var maps = template.Maps.OrderBy(m => m.StageOrder).ToList();
        maps[0].ChoosesBranch = true;
        maps[1].Branch = BenignBranch;
        maps[2].Branch = TreatmentBranch;
        maps[2].IsOptional = true;

        var pathway = PathwayCreation.FromTemplate(template, Guid.NewGuid(), null, Today.AddDays(7), PatientPathwayPriority.Medium, Now);

        pathway.Stages.OrderBy(s => s.StageOrder).Select(s => (s.IsOptional, s.ChoosesBranch, s.Branch)).Should().Equal(
            (false, true, null), (false, false, BenignBranch), (true, false, TreatmentBranch), (false, false, null));
        pathway.Stages.Should().AllSatisfy(s => s.IsSkipped.Should().BeFalse());
    }

    [TestCase(VisitRequirement.Required, false, false)]
    [TestCase(VisitRequirement.Required, true, true)]
    [TestCase(VisitRequirement.Suggested, false, true)]
    [TestCase(VisitRequirement.None, false, true)]
    public void CanSkipStage_RequiredVisit_NeedsOverrideStages(VisitRequirement requirement, bool canOverrideStages, bool expected)
    {
        var pathway = CreatePathway(Status.InProgress);
        pathway.StageAt(1).IsOptional = true;
        pathway.StageAt(1).VisitRequirement = requirement;

        PathwayWorkflow.CanSkipStage(pathway, pathway.StageAt(1), canOverrideStages).Should().Be(expected);
    }

    [Test]
    public void FinishStage_BranchNotChosenWithARequiredVisit_IsStillSkipped()
    {
        // The clinician chose the other branch: the stage isn't needed, whatever its visit (decision 113).
        var pathway = CreateBranchingPathway();
        pathway.StageAt(2).VisitRequirement = VisitRequirement.Required;

        new PathwayChange(pathway, Staff, Now).FinishStage(pathway.StageAt(1), branch: TreatmentBranch);

        pathway.StageAt(2).IsSkipped.Should().BeTrue();
    }
}
