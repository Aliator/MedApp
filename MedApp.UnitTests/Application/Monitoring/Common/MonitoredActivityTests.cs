using FluentAssertions;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.UnitTests.Application.Monitoring.Common;

/// <summary>Reading what a change did from the audit entries ProcedureChange really writes.</summary>
[TestFixture]
public sealed class MonitoredActivityTests
{
    [Test]
    public void From_ProcedureCreated_IsCreatedByTheActor()
    {
        var procedure = CreateProcedure(Status.Unassigned);
        var change = new ProcedureChange(procedure, Staff, Now);
        change.Audit(PatientProcedureAuditAction.Created, new { template = TemplateName });

        var activities = MonitoredActivity.From(procedure, change.AuditEntries);

        activities.Should().Equal(new MonitoredActivity(MonitoringEvent.ProcedureCreated, Staff.Username, Staff.UserId, Staff.Username));
    }

    [Test]
    public void From_LastStageFinished_IsTheStageOnly_NotTheAutomaticMoveToInReview()
    {
        var procedure = CreateProcedure(Status.InProgress, finishedStages: 2);
        var stage = procedure.StageAt(3);
        var change = new ProcedureChange(procedure, Staff, Now);
        change.FinishStage(stage);

        var activities = MonitoredActivity.From(procedure, change.AuditEntries);

        procedure.Status.Should().Be(Status.InReview);
        activities.Should().Equal(new MonitoredActivity(
            MonitoringEvent.StageFinished, Staff.Username, Staff.UserId, Staff.Username, ConsultationDefinitionId, ConsultationStage));
    }

    [Test]
    public void From_StageUnlocked_CarriesTheStagesDefinition()
    {
        var procedure = CreateProcedure(Status.InProgress, finishedStages: 2);
        var change = new ProcedureChange(procedure, Admin, Now);
        change.UnlockStage(procedure.StageAt(2));

        var activity = MonitoredActivity.From(procedure, change.AuditEntries).Should().ContainSingle().Subject;

        activity.Event.Should().Be(MonitoringEvent.StageUnlocked);
        activity.StageDefinitionId.Should().Be(ProcedureDefinitionId);
        activity.StageName.Should().Be(ProcedureStage);
        activity.SubjectUserId.Should().Be(Admin.UserId);
    }

    [TestCase(Status.Completed, MonitoringEvent.ProcedureCompleted)]
    [TestCase(Status.Blocked, MonitoringEvent.ProcedureBlocked)]
    [TestCase(Status.Cancelled, MonitoringEvent.ProcedureCancelled)]
    public void From_StatusChangedByStaff_IsThatEvent(Status to, MonitoringEvent expected)
    {
        var procedure = CreateProcedure(Status.InReview, finishedStages: 3);
        var change = new ProcedureChange(procedure, Staff, Now);
        change.ChangeStatus(to, Reason);

        MonitoredActivity.From(procedure, change.AuditEntries).Should().ContainSingle().Which.Event.Should().Be(expected);
    }

    [TestCase(Status.Archived)]
    [TestCase(Status.InProgress)]
    public void From_StatusChangeNoRuleWatches_IsNothing(Status to)
    {
        var procedure = CreateProcedure(Status.Completed, finishedStages: 3);
        var change = new ProcedureChange(procedure, Staff, Now);
        change.ChangeStatus(to, null);

        MonitoredActivity.From(procedure, change.AuditEntries).Should().BeEmpty();
    }

    [Test]
    public void From_Assigned_IsAboutWhoWasAssigned_NotWhoAssignedThem()
    {
        var procedure = CreateProcedure(Status.Unassigned);
        var change = new ProcedureChange(procedure, Admin, Now);
        change.Assign(Staff.UserId, Staff.Username);

        var activities = MonitoredActivity.From(procedure, change.AuditEntries);

        // The automatic move to Assigned is ignored.
        activities.Should().Equal(new MonitoredActivity(MonitoringEvent.ProcedureAssigned, Admin.Username, Staff.UserId, Staff.Username));
    }

    [Test]
    public void From_DueDateEdited_CarriesBothDates()
    {
        var procedure = CreateProcedure();
        var change = new ProcedureChange(procedure, Staff, Now);
        var from = new DateOnly(2026, 6, 22);
        var to = new DateOnly(2026, 6, 29);
        change.Audit(PatientProcedureAuditAction.DetailsUpdated, new
        {
            changes = new Dictionary<string, object>
            {
                ["title"] = new { from = "Old", to = "New" },
                ["dueDate"] = new { from, to },
            }
        });

        MonitoredActivity.From(procedure, change.AuditEntries).Should().Equal(new MonitoredActivity(
            MonitoringEvent.DueDateChanged, Staff.Username, Staff.UserId, Staff.Username, DueDateFrom: from, DueDateTo: to));
    }

    [Test]
    public void From_DueDateMovedAndPriorityChangedInOneEdit_CarriesThePriorityBefore()
    {
        var procedure = CreateProcedure(priority: PatientProcedurePriority.Low);
        var change = new ProcedureChange(procedure, Staff, Now);
        change.Audit(PatientProcedureAuditAction.DetailsUpdated, new
        {
            changes = new Dictionary<string, object>
            {
                ["dueDate"] = new { from = new DateOnly(2026, 6, 22), to = new DateOnly(2026, 7, 22) },
                ["priority"] = new { from = "Urgent", to = "Low" },
            }
        });

        MonitoredActivity.From(procedure, change.AuditEntries).Should().ContainSingle()
            .Which.PriorityBefore.Should().Be(PatientProcedurePriority.Urgent);
    }

    [Test]
    public void From_DetailsEditedWithoutTheDueDate_IsNothing()
    {
        var procedure = CreateProcedure();
        var change = new ProcedureChange(procedure, Staff, Now);
        change.Audit(PatientProcedureAuditAction.DetailsUpdated, new { changes = new { priority = new { from = "Low", to = "High" } } });

        MonitoredActivity.From(procedure, change.AuditEntries).Should().BeEmpty();
    }

    [Test]
    public void From_NotesSavedOnANewStage_IsNothing()
    {
        var procedure = CreateProcedure();
        var change = new ProcedureChange(procedure, Staff, Now);
        change.UpdateStageNotes(procedure.StageAt(1), "Synthetic notes.");

        // Started, notes saved, and the automatic move to In Progress: none is watched.
        change.AuditEntries.Should().HaveCount(3);
        MonitoredActivity.From(procedure, change.AuditEntries).Should().BeEmpty();
    }

    [Test]
    public void From_UnreadableDetails_SkipsThem()
    {
        var procedure = CreateProcedure();
        var change = new ProcedureChange(procedure, Staff, Now);
        change.Audit(PatientProcedureAuditAction.Assigned);
        change.Audit(PatientProcedureAuditAction.StatusChanged, new { from = "Assigned" });
        change.Audit(PatientProcedureAuditAction.DetailsUpdated, new { changes = new { dueDate = "2026-06-22" } });
        change.Audit(PatientProcedureAuditAction.Deleted);

        MonitoredActivity.From(procedure, change.AuditEntries).Should().BeEmpty();
    }
}
