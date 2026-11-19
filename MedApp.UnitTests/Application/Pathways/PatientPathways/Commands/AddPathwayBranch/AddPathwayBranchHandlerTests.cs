using System.Text.Json;
using AutoFixture;
using FluentAssertions;
using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.PatientPathways.Commands.AddPathwayBranch;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.AddPathwayBranch;

[TestFixture]
public sealed class AddPathwayBranchHandlerTests : PathwayHandlerTestBase
{
    private const string Reason = "Synthetic: the sample was insufficient.";
    private const string RepeatSample = "Repeat sample";
    private const string RepeatLab = "Repeat lab test";

    private static readonly Guid Lab = Guid.Parse("0000005a-0000-0000-0000-0000000000bb");
    private static readonly Guid RepeatDefinitionId = Guid.Parse("0000000d-0000-0000-0000-0000000000aa");
    private static readonly Guid LabDefinitionId = Guid.Parse("0000000d-0000-0000-0000-0000000000bb");

    private AddPathwayBranchHandler Handler => Fixture.Create<AddPathwayBranchHandler>();

    /// <summary>
    /// A chain of three made from a plan whose addable branch "Repeat" (a sample, then the lab's test) may follow the
    /// first stage only, once unless <paramref name="repeatable"/>; the plan's stage 2 is the lab's, so the branch's lab
    /// test is the lab's too.
    /// </summary>
    private PatientPathway GivenPathwayWithABranch(bool repeatable = false, int finishedStages = 1)
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: finishedStages);
        pathway.TeamId = TeamId;
        foreach (var (stage, order) in pathway.Stages.Select((s, i) => (s, i + 1)))
            stage.TemplateStageOrder = order;
        pathway.Plan = new PatientPathwayPlan(
            [
                Planned(1, ConsultationDefinitionId, ConsultationStage),
                Planned(2, LabDefinitionId, PathwayStage, ownerTeamId: Lab),
                Planned(3, ConsultationDefinitionId, ConsultationStage)
            ],
            [new(1, 2), new(2, 3)],
            [
                new PathwayPlanBranch(Guid.NewGuid(), "Repeat",
                    [Planned(0, RepeatDefinitionId, RepeatSample), Planned(0, LabDefinitionId, RepeatLab)], [1], repeatable)
            ]);
        return GivenPathway(pathway);
    }

    private static AddPathwayBranchCommand Add(PatientPathway pathway, Guid? afterStageId = null) =>
        new(pathway.Id, pathway.Plan!.Branches[0].Id, afterStageId ?? pathway.StageAt(1).Id, $"  {Reason} ", Staff);

    [Test]
    public async Task Handle_AnAllowedBranch_AddsItsStagesAfterTheStageOnARouteOfTheirOwn()
    {
        var pathway = GivenPathwayWithABranch();

        var response = await Handler.Handle(Add(pathway), CancellationToken.None);

        var added = pathway.Stages.Where(s => s.BranchAdditionId is not null).OrderBy(s => s.StageOrder).ToList();
        added.Select(s => (s.Name, s.StageOrder, s.OwnerTeamId)).Should().Equal((RepeatSample, 4, (Guid?)null), (RepeatLab, 5, Lab));
        PathwayGraph.Predecessors(pathway, added[0]).Should().Equal(pathway.StageAt(1));
        PathwayGraph.Predecessors(pathway, added[1]).Should().Equal(added[0]);
        // Side by side with what's left: the branch's first stage is current with the pathway's own.
        response.CurrentStageIds.Should().Equal(pathway.StageAt(2).Id, added[0].Id);
        response.BranchAdditions.Should().ContainSingle().Which.Reason.Should().Be(Reason);
        response.AddableBranches.Single().TimesAdded.Should().Be(1);
        response.AddableBranches.Single().AfterStageIds.Should().BeEmpty();
        response.CanAddBranch.Should().BeFalse();

        var change = SavedChanges.Single();
        change.NewStages.Should().HaveCount(2);
        // Its chain written first (a pathway from before links), then the branch's two.
        change.NewLinks.Should().HaveCount(4);
        change.NewBranchAdditions.Should().ContainSingle().Which.PatientId.Should().Be(pathway.PatientId);
        var entry = SavedAudit.Single(e => e.Action == PatientPathwayAuditAction.BranchAdded);
        entry.Details.Should().NotContain(Reason);
        using var details = JsonDocument.Parse(entry.Details);
        details.RootElement.GetProperty("branch").GetString().Should().Be("Repeat");
        details.RootElement.GetProperty("stageIds").EnumerateArray().Should().HaveCount(2);
    }

    [Test]
    public async Task Handle_AfterAStageItMayNotFollow_IsAConflictWithoutSaving()
    {
        var pathway = GivenPathwayWithABranch();

        var act = () => Handler.Handle(Add(pathway, pathway.StageAt(2).Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PathwayBranches.CantGoThereMessage("Repeat", PathwayStage));
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_AddedOnceAndNotRepeatable_IsAConflictTheSecondTime_AndARepeatableOneGoesAgain()
    {
        var once = GivenPathwayWithABranch();
        await Handler.Handle(Add(once), CancellationToken.None);
        var again = () => Handler.Handle(Add(once), CancellationToken.None);
        await again.Should().ThrowAsync<ConflictException>();

        var repeatable = GivenPathwayWithABranch(repeatable: true);
        await Handler.Handle(Add(repeatable), CancellationToken.None);
        var response = await Handler.Handle(Add(repeatable), CancellationToken.None);

        response.BranchAdditions.Should().HaveCount(2);
    }

    [Test]
    public async Task Handle_ABranchNotTheTemplates_IsRefusedOnTheBranch()
    {
        var pathway = GivenPathwayWithABranch();

        var act = () => Handler.Handle(
            new AddPathwayBranchCommand(pathway.Id, Guid.NewGuid(), pathway.StageAt(1).Id, Reason, Staff), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainSingle(e =>
            e.PropertyName == "TemplateBranchId" && e.ErrorMessage == PathwayBranches.NotTheTemplatesMessage);
    }

    [Test]
    public async Task Handle_OnHold_IsAConflict()
    {
        var pathway = GivenPathwayWithABranch();
        pathway.Status = Status.Blocked;

        var act = () => Handler.Handle(Add(pathway), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PathwayBranches.OnHoldOrClosedMessage);
    }

    [Test]
    public async Task Handle_AfterAFinishedStageWhoseFirstStageIsTheLabs_HandsItOver()
    {
        var pathway = GivenPathwayWithABranch();
        pathway.Plan = pathway.Plan! with
        {
            Branches = [pathway.Plan.Branches[0] with { Stages = [Planned(0, LabDefinitionId, RepeatLab)] }]
        };

        await Handler.Handle(Add(pathway), CancellationToken.None);

        var added = pathway.Stages.Single(s => s.BranchAdditionId is not null);
        added.HandedOverFromTeamId.Should().Be(TeamId);
        SavedChanges.Single().Handovers.Should().ContainSingle(h => h.ToTeamId == Lab);
    }
}
