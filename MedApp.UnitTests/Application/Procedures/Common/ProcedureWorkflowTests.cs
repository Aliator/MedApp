using FluentAssertions;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;
using MedApp.Domain.Appointments;

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

    [Test]
    public void AllowedStatusChanges_BlockedWithoutBlockedFromStatus_OffersAssignedWhenSomeoneIsAssigned()
    {
        var procedure = CreateProcedure(Status.Blocked);
        procedure.BlockedFromStatus = null;

        ProcedureWorkflow.AllowedStatusChanges(procedure).Should().Equal(Status.Assigned, Status.Cancelled);
    }

    [Test]
    public void AllowedStatusChanges_BlockedWithoutBlockedFromStatusOrAssignees_OffersUnassigned()
    {
        var procedure = CreateProcedure(Status.Blocked);
        procedure.BlockedFromStatus = null;
        procedure.Assignments.Clear();

        ProcedureWorkflow.AllowedStatusChanges(procedure).Should().Equal(Status.Unassigned, Status.Cancelled);
    }

    [Test]
    public void HasStarted_NoStageStarted_IsFalse()
    {
        ProcedureWorkflow.HasStarted(CreateProcedure(Status.Assigned)).Should().BeFalse();
    }

    [Test]
    public void HasStarted_FirstStageStartedButNotFinished_IsTrue()
    {
        var procedure = CreateProcedure(Status.InProgress);
        procedure.StageAt(1).StartedAtUtc = Now.AddHours(-1);

        ProcedureWorkflow.HasStarted(procedure).Should().BeTrue();
    }

    [Test]
    public void HasStarted_StageFinishedWithoutAStartTime_IsTrue()
    {
        var procedure = CreateProcedure(Status.InProgress);
        procedure.StageAt(1).IsCompleted = true;

        ProcedureWorkflow.HasStarted(procedure).Should().BeTrue();
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
    public void CanEditStage_UnfinishedStageOfAProcedureWithEveryStageFinished_IsFalse()
    {
        // No current stage, so no unfinished stage (here one not in this procedure) can be the one being worked on.
        var procedure = CreateProcedure(Status.InReview, finishedStages: 3);
        var strayStage = CreateStage(Guid.NewGuid(), 1, ConsultationDefinitionId, ConsultationStage);

        ProcedureWorkflow.CanEditStage(procedure, strayStage).Should().BeFalse();
        ProcedureWorkflow.WhyStageCantBeEdited(procedure, strayStage).Should().StartWith("Stages are worked in order");
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

    private static Appointment StageAppointment(PatientProcedureStage stage, AppointmentStatus status) => new()
    {
        Id = Guid.NewGuid(),
        StageId = stage.Id,
        Status = status
    };

    [Test]
    public void WhyStageNeedsVisit_RequiredWithoutAnAttendedVisit_SaysToBookItAndThatAnAdminCanFinishIt()
    {
        var stage = CreateProcedure().StageAt(1);
        stage.VisitRequirement = VisitRequirement.Required;

        ProcedureWorkflow.WhyStageNeedsVisit(stage, [StageAppointment(stage, AppointmentStatus.Booked)])
            .Should().Be($"The '{ConsultationStage}' stage needs a visit before it can be finished: book it, and mark it Attended once it's happened. An admin can finish it without one.");
    }

    [Test]
    public void WhyStageNeedsVisit_RequiredWithAnAttendedVisit_IsNull()
    {
        var stage = CreateProcedure().StageAt(1);
        stage.VisitRequirement = VisitRequirement.Required;

        ProcedureWorkflow.WhyStageNeedsVisit(stage, [StageAppointment(stage, AppointmentStatus.Attended)]).Should().BeNull();
    }

    [Test]
    public void WhyStageNeedsVisit_AttendedVisitForAnotherStage_DoesNotCount()
    {
        var procedure = CreateProcedure();
        var stage = procedure.StageAt(1);
        stage.VisitRequirement = VisitRequirement.Required;

        ProcedureWorkflow.WhyStageNeedsVisit(stage, [StageAppointment(procedure.StageAt(2), AppointmentStatus.Attended)])
            .Should().NotBeNull();
    }

    [TestCase(VisitRequirement.None)]
    [TestCase(VisitRequirement.Suggested)]
    public void WhyStageNeedsVisit_NotRequired_IsNull(VisitRequirement requirement)
    {
        var stage = CreateProcedure().StageAt(1);
        stage.VisitRequirement = requirement;

        ProcedureWorkflow.WhyStageNeedsVisit(stage, []).Should().BeNull();
    }

    [Test]
    public void WhyStageNeedsVisit_FinishedBeforeAndUnlockedByAnAdmin_IsNull()
    {
        var procedure = CreateProcedure(Status.InProgress, finishedStages: 1);
        var stage = procedure.StageAt(1);
        stage.VisitRequirement = VisitRequirement.Required;
        stage.IsLocked = false;

        ProcedureWorkflow.WhyStageNeedsVisit(stage, []).Should().BeNull();
    }

    [TestCase(VisitRequirement.None, false)]
    [TestCase(VisitRequirement.Suggested, true)]
    [TestCase(VisitRequirement.Required, true)]
    public void FinishesWithoutVisit_NoAttendedVisit_DependsOnTheRequirement(VisitRequirement requirement, bool expected)
    {
        var stage = CreateProcedure().StageAt(1);
        stage.VisitRequirement = requirement;

        ProcedureWorkflow.FinishesWithoutVisit(stage, [StageAppointment(stage, AppointmentStatus.DidNotAttend)]).Should().Be(expected);
    }

    [Test]
    public void FinishesWithoutVisit_AttendedVisit_IsFalse()
    {
        var stage = CreateProcedure().StageAt(1);
        stage.VisitRequirement = VisitRequirement.Suggested;

        ProcedureWorkflow.FinishesWithoutVisit(stage, [StageAppointment(stage, AppointmentStatus.Attended)]).Should().BeFalse();
    }

    [Test]
    public void FinishesWithoutVisit_FinishedBefore_IsFalse()
    {
        var procedure = CreateProcedure(Status.InProgress, finishedStages: 1);
        procedure.StageAt(1).VisitRequirement = VisitRequirement.Suggested;

        ProcedureWorkflow.FinishesWithoutVisit(procedure.StageAt(1), []).Should().BeFalse();
    }
}
