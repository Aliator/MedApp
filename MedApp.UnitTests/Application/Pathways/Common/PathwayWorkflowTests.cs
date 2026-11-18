using FluentAssertions;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;
using MedApp.Domain.Appointments;

namespace MedApp.UnitTests.Application.Pathways.Common;

[TestFixture]
public sealed class PathwayWorkflowTests
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
        PathwayWorkflow.AllowedStatusChanges(CreatePathway(status)).Should().Equal(expected);
    }

    [Test]
    public void AllowedStatusChanges_Blocked_OffersTheStatusItWasBlockedFromAndCancelling()
    {
        var pathway = CreatePathway(Status.Blocked);
        pathway.BlockedFromStatus = Status.InProgress;

        PathwayWorkflow.AllowedStatusChanges(pathway).Should().Equal(Status.InProgress, Status.Cancelled);
    }

    [Test]
    public void AllowedStatusChanges_BlockedWithoutBlockedFromStatus_OffersAssignedWhenSomeoneIsAssigned()
    {
        var pathway = CreatePathway(Status.Blocked);
        pathway.BlockedFromStatus = null;

        PathwayWorkflow.AllowedStatusChanges(pathway).Should().Equal(Status.Assigned, Status.Cancelled);
    }

    [Test]
    public void AllowedStatusChanges_BlockedWithoutBlockedFromStatusOrAssignees_OffersUnassigned()
    {
        var pathway = CreatePathway(Status.Blocked);
        pathway.BlockedFromStatus = null;
        pathway.Assignments.Clear();

        PathwayWorkflow.AllowedStatusChanges(pathway).Should().Equal(Status.Unassigned, Status.Cancelled);
    }

    [Test]
    public void HasStarted_NoStageStarted_IsFalse()
    {
        PathwayWorkflow.HasStarted(CreatePathway(Status.Assigned)).Should().BeFalse();
    }

    [Test]
    public void HasStarted_FirstStageStartedButNotFinished_IsTrue()
    {
        var pathway = CreatePathway(Status.InProgress);
        pathway.StageAt(1).StartedAtUtc = Now.AddHours(-1);

        PathwayWorkflow.HasStarted(pathway).Should().BeTrue();
    }

    [Test]
    public void HasStarted_StageFinishedWithoutAStartTime_IsTrue()
    {
        var pathway = CreatePathway(Status.InProgress);
        pathway.StageAt(1).IsCompleted = true;

        PathwayWorkflow.HasStarted(pathway).Should().BeTrue();
    }

    [TestCase(Status.Assigned, -1, true)]
    [TestCase(Status.InReview, -1, true)]
    [TestCase(Status.Assigned, 1, false)]
    [TestCase(Status.Blocked, -1, false)]
    [TestCase(Status.Completed, -1, false)]
    [TestCase(Status.Cancelled, -1, false)]
    public void IsOverdue_StatusAndDueDate_OnlyOpenPathwaysPastTheirDueDate(Status status, int dueInDays, bool expected)
    {
        var pathway = CreatePathway(status, dueDateUtc: Now.AddDays(dueInDays));

        PathwayWorkflow.IsOverdue(pathway, Now).Should().Be(expected);
    }

    [Test]
    public void CurrentStage_SomeFinished_IsTheFirstUnfinishedStage()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 1);

        PathwayWorkflow.CurrentStage(pathway).Should().BeSameAs(pathway.StageAt(2));
    }

    [Test]
    public void CurrentStage_AllFinished_IsNull()
    {
        PathwayWorkflow.CurrentStage(CreatePathway(Status.InReview, finishedStages: 3)).Should().BeNull();
    }

    [Test]
    public void CanEditStage_CurrentStage_IsTrue()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 1);

        PathwayWorkflow.CanEditStage(pathway, pathway.StageAt(2)).Should().BeTrue();
    }

    [Test]
    public void WhyStageCantBeEdited_LaterStage_SaysToFinishTheCurrentStageFirst()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 1);

        PathwayWorkflow.WhyStageCantBeEdited(pathway, pathway.StageAt(3))
            .Should().Contain($"finish the '{PathwayStage}' stage first");
    }

    [Test]
    public void WhyStageCantBeEdited_FinishedLockedStage_SaysAnAdminCanUnlockIt()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 1);

        PathwayWorkflow.WhyStageCantBeEdited(pathway, pathway.StageAt(1)).Should().Contain("An admin can unlock it");
    }

    [Test]
    public void CanEditStage_UnfinishedStageOfAPathwayWithEveryStageFinished_IsFalse()
    {
        // No current stage, so no unfinished stage (here one not in this pathway) can be the one being worked on.
        var pathway = CreatePathway(Status.InReview, finishedStages: 3);
        var strayStage = CreateStage(Guid.NewGuid(), 1, ConsultationDefinitionId, ConsultationStage);

        PathwayWorkflow.CanEditStage(pathway, strayStage).Should().BeFalse();
        PathwayWorkflow.WhyStageCantBeEdited(pathway, strayStage).Should().StartWith("Stages are worked in order");
    }

    [Test]
    public void CanEditStage_FinishedStageUnlockedByAnAdmin_IsTrue()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 2);
        pathway.StageAt(1).IsLocked = false;

        PathwayWorkflow.CanEditStage(pathway, pathway.StageAt(1)).Should().BeTrue();
    }

    [TestCase(Status.Blocked)]
    [TestCase(Status.Cancelled)]
    [TestCase(Status.Completed)]
    public void CanEditStage_PathwayNotOpen_IsFalse(Status status)
    {
        var pathway = CreatePathway(status);

        PathwayWorkflow.CanEditStage(pathway, pathway.StageAt(1)).Should().BeFalse();
    }

    [TestCase(Status.InProgress, true)]
    [TestCase(Status.InReview, true)]
    [TestCase(Status.Completed, true)]
    [TestCase(Status.Blocked, false)]
    [TestCase(Status.Cancelled, false)]
    [TestCase(Status.Archived, false)]
    public void CanUnlockStage_FinishedStage_DependsOnThePathwayStatus(Status status, bool expected)
    {
        var pathway = CreatePathway(status, finishedStages: 1);

        PathwayWorkflow.CanUnlockStage(pathway, pathway.StageAt(1)).Should().Be(expected);
    }

    [Test]
    public void CanUnlockStage_UnfinishedStage_IsFalse()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 1);

        PathwayWorkflow.CanUnlockStage(pathway, pathway.StageAt(2)).Should().BeFalse();
    }

    [TestCase(Status.Cancelled, true)]
    [TestCase(Status.Archived, true)]
    [TestCase(Status.Completed, false)]
    [TestCase(Status.InProgress, false)]
    public void CanBeDeleted_Status_OnlyCancelledOrArchived(Status status, bool expected)
    {
        PathwayWorkflow.CanBeDeleted(CreatePathway(status)).Should().Be(expected);
    }

    [TestCase(Status.Unassigned, "unassigned")]
    [TestCase(Status.Assigned, "assigned")]
    [TestCase(Status.InProgress, "in progress")]
    [TestCase(Status.Cancelled, "cancelled")]
    [TestCase(Status.Archived, "archived")]
    public void Describe_Status_IsItsNameInLowerCaseWords(Status status, string expected)
    {
        PathwayWorkflow.Describe(status).Should().Be(expected);
    }

    // Three statuses are shown by another name than the one stored (docs/glossary.md).
    [TestCase(Status.InReview, "awaiting review")]
    [TestCase(Status.Blocked, "on hold")]
    [TestCase(Status.Completed, "discharged")]
    public void Describe_StatusShownByAnotherName_IsTheNamePeopleSee(Status status, string expected)
    {
        PathwayWorkflow.Describe(status).Should().Be(expected);
    }

    private static Appointment StageAppointment(PatientPathwayStage stage, AppointmentStatus status) => new()
    {
        Id = Guid.NewGuid(),
        StageId = stage.Id,
        Status = status
    };

    [Test]
    public void WhyStageNeedsVisit_RequiredWithoutAnAttendedVisit_SaysToBookItAndThatAnAdminCanFinishIt()
    {
        var stage = CreatePathway().StageAt(1);
        stage.VisitRequirement = VisitRequirement.Required;

        PathwayWorkflow.WhyStageNeedsVisit(stage, [StageAppointment(stage, AppointmentStatus.Booked)])
            .Should().Be($"The '{ConsultationStage}' stage needs a visit before it can be finished: book it, and mark it Attended once it's happened. An admin can finish it without one.");
    }

    [Test]
    public void WhyStageNeedsVisit_RequiredWithAnAttendedVisit_IsNull()
    {
        var stage = CreatePathway().StageAt(1);
        stage.VisitRequirement = VisitRequirement.Required;

        PathwayWorkflow.WhyStageNeedsVisit(stage, [StageAppointment(stage, AppointmentStatus.Attended)]).Should().BeNull();
    }

    [Test]
    public void WhyStageNeedsVisit_AttendedVisitForAnotherStage_DoesNotCount()
    {
        var pathway = CreatePathway();
        var stage = pathway.StageAt(1);
        stage.VisitRequirement = VisitRequirement.Required;

        PathwayWorkflow.WhyStageNeedsVisit(stage, [StageAppointment(pathway.StageAt(2), AppointmentStatus.Attended)])
            .Should().NotBeNull();
    }

    [TestCase(VisitRequirement.None)]
    [TestCase(VisitRequirement.Suggested)]
    public void WhyStageNeedsVisit_NotRequired_IsNull(VisitRequirement requirement)
    {
        var stage = CreatePathway().StageAt(1);
        stage.VisitRequirement = requirement;

        PathwayWorkflow.WhyStageNeedsVisit(stage, []).Should().BeNull();
    }

    [Test]
    public void WhyStageNeedsVisit_FinishedBeforeAndUnlockedByAnAdmin_IsNull()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 1);
        var stage = pathway.StageAt(1);
        stage.VisitRequirement = VisitRequirement.Required;
        stage.IsLocked = false;

        PathwayWorkflow.WhyStageNeedsVisit(stage, []).Should().BeNull();
    }

    [TestCase(VisitRequirement.None, false)]
    [TestCase(VisitRequirement.Suggested, true)]
    [TestCase(VisitRequirement.Required, true)]
    public void FinishesWithoutVisit_NoAttendedVisit_DependsOnTheRequirement(VisitRequirement requirement, bool expected)
    {
        var stage = CreatePathway().StageAt(1);
        stage.VisitRequirement = requirement;

        PathwayWorkflow.FinishesWithoutVisit(stage, [StageAppointment(stage, AppointmentStatus.DidNotAttend)]).Should().Be(expected);
    }

    [Test]
    public void FinishesWithoutVisit_AttendedVisit_IsFalse()
    {
        var stage = CreatePathway().StageAt(1);
        stage.VisitRequirement = VisitRequirement.Suggested;

        PathwayWorkflow.FinishesWithoutVisit(stage, [StageAppointment(stage, AppointmentStatus.Attended)]).Should().BeFalse();
    }

    [Test]
    public void FinishesWithoutVisit_FinishedBefore_IsFalse()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 1);
        pathway.StageAt(1).VisitRequirement = VisitRequirement.Suggested;

        PathwayWorkflow.FinishesWithoutVisit(pathway.StageAt(1), []).Should().BeFalse();
    }
}
