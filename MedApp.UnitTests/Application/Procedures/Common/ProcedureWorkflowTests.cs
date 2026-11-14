using FluentAssertions;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.UnitTests.Application.Procedures.Common;

[TestFixture]
public sealed class ProcedureWorkflowTests
{
    [TestCase(Status.Unassigned, new[] { Status.Blocked, Status.Cancelled })]
    [TestCase(Status.Assigned, new[] { Status.Blocked, Status.Cancelled })]
    [TestCase(Status.InProgress, new[] { Status.Blocked, Status.Cancelled })]
    [TestCase(Status.InReview, new[] { Status.Completed, Status.Blocked, Status.Cancelled })]
    [TestCase(Status.Completed, new[] { Status.Archived })]
    [TestCase(Status.Cancelled, new[] { Status.Archived })]
    [TestCase(Status.Archived, new Status[0])]
    public void AllowedStatusChanges_FromStatus_ReturnsTheManualMoves(Status status, Status[] expected)
    {
        ProcedureWorkflow.AllowedStatusChanges(CreateProcedure(status)).Should().Equal(expected);
    }

    [Test]
    public void AllowedStatusChanges_Blocked_OffersTheStatusItWasBlockedFromAndCancelling()
    {
        var procedure = CreateProcedure(Status.Blocked);
        procedure.BlockedFromStatus = Status.InProgress;

        ProcedureWorkflow.AllowedStatusChanges(procedure).Should().Equal(Status.InProgress, Status.Cancelled);
    }

    [TestCase(Status.Assigned, -1, true)]
    [TestCase(Status.InReview, -1, true)]
    [TestCase(Status.Assigned, 1, false)]
    [TestCase(Status.Blocked, -1, false)]
    [TestCase(Status.Completed, -1, false)]
    [TestCase(Status.Cancelled, -1, false)]
    public void IsOverdue_StatusAndDueDate_OnlyOpenProceduresPastTheirDueDate(Status status, int dueInDays, bool expected)
    {
        var procedure = CreateProcedure(status, dueDateUtc: Now.AddDays(dueInDays));

        ProcedureWorkflow.IsOverdue(procedure, Now).Should().Be(expected);
    }

    [Test]
    public void CurrentStage_SomeFinished_IsTheFirstUnfinishedStage()
    {
        var procedure = CreateProcedure(Status.InProgress, finishedStages: 1);

        ProcedureWorkflow.CurrentStage(procedure).Should().BeSameAs(procedure.StageAt(2));
    }

    [Test]
    public void CurrentStage_AllFinished_IsNull()
    {
        ProcedureWorkflow.CurrentStage(CreateProcedure(Status.InReview, finishedStages: 3)).Should().BeNull();
    }

    [Test]
    public void CanEditStage_CurrentStage_IsTrue()
    {
        var procedure = CreateProcedure(Status.InProgress, finishedStages: 1);

        ProcedureWorkflow.CanEditStage(procedure, procedure.StageAt(2)).Should().BeTrue();
    }

    [Test]
    public void WhyStageCantBeEdited_LaterStage_SaysToFinishTheCurrentStageFirst()
    {
        var procedure = CreateProcedure(Status.InProgress, finishedStages: 1);

        ProcedureWorkflow.WhyStageCantBeEdited(procedure, procedure.StageAt(3))
            .Should().Contain($"finish the '{ProcedureStage}' stage first");
    }

    [Test]
    public void WhyStageCantBeEdited_FinishedLockedStage_SaysAnAdminCanUnlockIt()
    {
        var procedure = CreateProcedure(Status.InProgress, finishedStages: 1);

        ProcedureWorkflow.WhyStageCantBeEdited(procedure, procedure.StageAt(1)).Should().Contain("An admin can unlock it");
    }

    [Test]
    public void CanEditStage_FinishedStageUnlockedByAnAdmin_IsTrue()
    {
        var procedure = CreateProcedure(Status.InProgress, finishedStages: 2);
        procedure.StageAt(1).IsLocked = false;

        ProcedureWorkflow.CanEditStage(procedure, procedure.StageAt(1)).Should().BeTrue();
    }

    [TestCase(Status.Blocked)]
    [TestCase(Status.Cancelled)]
    [TestCase(Status.Completed)]
    public void CanEditStage_ProcedureNotOpen_IsFalse(Status status)
    {
        var procedure = CreateProcedure(status);

        ProcedureWorkflow.CanEditStage(procedure, procedure.StageAt(1)).Should().BeFalse();
    }

    [TestCase(Status.InProgress, true)]
    [TestCase(Status.InReview, true)]
    [TestCase(Status.Completed, true)]
    [TestCase(Status.Blocked, false)]
    [TestCase(Status.Cancelled, false)]
    [TestCase(Status.Archived, false)]
    public void CanUnlockStage_FinishedStage_DependsOnTheProcedureStatus(Status status, bool expected)
    {
        var procedure = CreateProcedure(status, finishedStages: 1);

        ProcedureWorkflow.CanUnlockStage(procedure, procedure.StageAt(1)).Should().Be(expected);
    }

    [Test]
    public void CanUnlockStage_UnfinishedStage_IsFalse()
    {
        var procedure = CreateProcedure(Status.InProgress, finishedStages: 1);

        ProcedureWorkflow.CanUnlockStage(procedure, procedure.StageAt(2)).Should().BeFalse();
    }

    [TestCase(Status.Cancelled, true)]
    [TestCase(Status.Archived, true)]
    [TestCase(Status.Completed, false)]
    [TestCase(Status.InProgress, false)]
    public void CanBeDeleted_Status_OnlyCancelledOrArchived(Status status, bool expected)
    {
        ProcedureWorkflow.CanBeDeleted(CreateProcedure(status)).Should().Be(expected);
    }

    [Test]
    public void Describe_MultiWordStatus_IsLowerCaseWords()
    {
        ProcedureWorkflow.Describe(Status.InReview).Should().Be("in review");
    }
}
