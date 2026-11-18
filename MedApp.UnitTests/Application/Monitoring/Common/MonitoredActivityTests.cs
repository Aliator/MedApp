using FluentAssertions;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Monitoring.Common;

/// <summary>Reading what a change did from the audit entries PathwayChange really writes.</summary>
[TestFixture]
public sealed class MonitoredActivityTests
{
    [Test]
    public void From_PathwayCreated_IsCreatedByTheActor()
    {
        var pathway = CreatePathway(Status.Unassigned);
        var change = new PathwayChange(pathway, Staff, Now);
        change.Audit(PatientPathwayAuditAction.Created, new { template = TemplateName });

        var activities = MonitoredActivity.From(pathway, change.AuditEntries);

        activities.Should().Equal(new MonitoredActivity(MonitoringEvent.PathwayCreated, Staff.Username, Staff.UserId, Staff.Username));
    }

    [Test]
    public void From_LastStageFinished_IsTheStageOnly_NotTheAutomaticMoveToInReview()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 2);
        var stage = pathway.StageAt(3);
        var change = new PathwayChange(pathway, Staff, Now);
        change.FinishStage(stage);

        var activities = MonitoredActivity.From(pathway, change.AuditEntries);

        pathway.Status.Should().Be(Status.InReview);
        activities.Should().Equal(new MonitoredActivity(
            MonitoringEvent.StageFinished, Staff.Username, Staff.UserId, Staff.Username, ConsultationDefinitionId, ConsultationStage));
    }

    [Test]
    public void From_RequiredStageFinishedWithoutItsVisit_IsThatFirst_ThenStageFinished()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 2);
        var change = new PathwayChange(pathway, Staff, Now);
        change.FinishStage(pathway.StageAt(3), new FinishedWithoutVisit(VisitRequirement.Required, WithoutVisitReason));

        var activities = MonitoredActivity.From(pathway, change.AuditEntries);

        activities.Should().Equal(
            new MonitoredActivity(MonitoringEvent.StageFinishedWithoutVisit, Staff.Username, Staff.UserId, Staff.Username,
                ConsultationDefinitionId, ConsultationStage),
            new MonitoredActivity(MonitoringEvent.StageFinished, Staff.Username, Staff.UserId, Staff.Username,
                ConsultationDefinitionId, ConsultationStage));
    }

    /// <summary>A Suggested visit can be left out by anyone, after confirming: that's not the bypass.</summary>
    [Test]
    public void From_SuggestedStageFinishedWithoutItsVisit_IsOnlyStageFinished()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 2);
        var change = new PathwayChange(pathway, Staff, Now);
        change.FinishStage(pathway.StageAt(3), new FinishedWithoutVisit(VisitRequirement.Suggested, null));

        MonitoredActivity.From(pathway, change.AuditEntries).Should().ContainSingle()
            .Which.Event.Should().Be(MonitoringEvent.StageFinished);
    }

    [Test]
    public void From_StageUnlocked_CarriesTheStagesDefinition()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 2);
        var change = new PathwayChange(pathway, Admin, Now);
        change.UnlockStage(pathway.StageAt(2));

        var activity = MonitoredActivity.From(pathway, change.AuditEntries).Should().ContainSingle().Subject;

        activity.Event.Should().Be(MonitoringEvent.StageUnlocked);
        activity.StageDefinitionId.Should().Be(PathwayDefinitionId);
        activity.StageName.Should().Be(PathwayStage);
        activity.SubjectUserId.Should().Be(Admin.UserId);
    }

    [TestCase(Status.Completed, MonitoringEvent.PathwayCompleted)]
    [TestCase(Status.Blocked, MonitoringEvent.PathwayBlocked)]
    [TestCase(Status.Cancelled, MonitoringEvent.PathwayCancelled)]
    public void From_StatusChangedByStaff_IsThatEvent(Status to, MonitoringEvent expected)
    {
        var pathway = CreatePathway(Status.InReview, finishedStages: 3);
        var change = new PathwayChange(pathway, Staff, Now);
        change.ChangeStatus(to, Reason);

        MonitoredActivity.From(pathway, change.AuditEntries).Should().ContainSingle().Which.Event.Should().Be(expected);
    }

    [TestCase(Status.Archived)]
    [TestCase(Status.InProgress)]
    public void From_StatusChangeNoRuleWatches_IsNothing(Status to)
    {
        var pathway = CreatePathway(Status.Completed, finishedStages: 3);
        var change = new PathwayChange(pathway, Staff, Now);
        change.ChangeStatus(to, null);

        MonitoredActivity.From(pathway, change.AuditEntries).Should().BeEmpty();
    }

    [Test]
    public void From_Assigned_IsAboutWhoWasAssigned_NotWhoAssignedThem()
    {
        var pathway = CreatePathway(Status.Unassigned);
        var change = new PathwayChange(pathway, Admin, Now);
        change.Assign(Staff.UserId, Staff.Username);

        var activities = MonitoredActivity.From(pathway, change.AuditEntries);

        // The automatic move to Assigned is ignored.
        activities.Should().Equal(new MonitoredActivity(MonitoringEvent.PathwayAssigned, Admin.Username, Staff.UserId, Staff.Username));
    }

    [Test]
    public void From_DueDateEdited_CarriesBothDates()
    {
        var pathway = CreatePathway();
        var change = new PathwayChange(pathway, Staff, Now);
        var from = new DateOnly(2026, 6, 22);
        var to = new DateOnly(2026, 6, 29);
        change.Audit(PatientPathwayAuditAction.DetailsUpdated, new
        {
            changes = new Dictionary<string, object>
            {
                ["title"] = new { from = "Old", to = "New" },
                ["dueDate"] = new { from, to },
            }
        });

        MonitoredActivity.From(pathway, change.AuditEntries).Should().Equal(new MonitoredActivity(
            MonitoringEvent.DueDateChanged, Staff.Username, Staff.UserId, Staff.Username, DueDateFrom: from, DueDateTo: to));
    }

    [Test]
    public void From_DueDateMovedAndPriorityChangedInOneEdit_CarriesThePriorityBefore()
    {
        var pathway = CreatePathway(priority: PatientPathwayPriority.Low);
        var change = new PathwayChange(pathway, Staff, Now);
        change.Audit(PatientPathwayAuditAction.DetailsUpdated, new
        {
            changes = new Dictionary<string, object>
            {
                ["dueDate"] = new { from = new DateOnly(2026, 6, 22), to = new DateOnly(2026, 7, 22) },
                ["priority"] = new { from = "Urgent", to = "Low" },
            }
        });

        MonitoredActivity.From(pathway, change.AuditEntries).Should().ContainSingle()
            .Which.PriorityBefore.Should().Be(PatientPathwayPriority.Urgent);
    }

    [Test]
    public void From_DetailsEditedWithoutTheDueDate_IsNothing()
    {
        var pathway = CreatePathway();
        var change = new PathwayChange(pathway, Staff, Now);
        change.Audit(PatientPathwayAuditAction.DetailsUpdated, new { changes = new { priority = new { from = "Low", to = "High" } } });

        MonitoredActivity.From(pathway, change.AuditEntries).Should().BeEmpty();
    }

    [Test]
    public void From_NotesSavedOnANewStage_IsNothing()
    {
        var pathway = CreatePathway();
        var change = new PathwayChange(pathway, Staff, Now);
        change.UpdateStageNotes(pathway.StageAt(1), "Synthetic notes.");

        // Started, notes saved, and the automatic move to In Progress: none is watched.
        change.AuditEntries.Should().HaveCount(3);
        MonitoredActivity.From(pathway, change.AuditEntries).Should().BeEmpty();
    }

    [Test]
    public void From_UnreadableDetails_SkipsThem()
    {
        var pathway = CreatePathway();
        var change = new PathwayChange(pathway, Staff, Now);
        change.Audit(PatientPathwayAuditAction.Assigned);
        change.Audit(PatientPathwayAuditAction.StatusChanged, new { from = "Assigned" });
        change.Audit(PatientPathwayAuditAction.DetailsUpdated, new { changes = new { dueDate = "2026-06-22" } });
        change.Audit(PatientPathwayAuditAction.Deleted);

        MonitoredActivity.From(pathway, change.AuditEntries).Should().BeEmpty();
    }
}
