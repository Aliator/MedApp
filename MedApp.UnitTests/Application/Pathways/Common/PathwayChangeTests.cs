using FluentAssertions;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;
using System.Text.Json;
using MedApp.Domain.Appointments;

namespace MedApp.UnitTests.Application.Pathways.Common;

[TestFixture]
public sealed class PathwayChangeTests
{
    private static PathwayChange ChangeOf(PatientPathway pathway, PathwayActor? actor = null) =>
        new(pathway, actor ?? Staff, Now);

    [Test]
    public void StartStage_AssignedPathway_StartsTheStageAndMovesToInProgress()
    {
        var pathway = CreatePathway(Status.Assigned);
        var change = ChangeOf(pathway);

        change.StartStage(pathway.StageAt(1));

        pathway.StageAt(1).StartedAtUtc.Should().Be(Now);
        pathway.StageAt(1).StartedByUserId.Should().Be(Staff.UserId);
        pathway.Status.Should().Be(Status.InProgress);
        pathway.LastUpdated.Should().Be(Now);
        change.AuditEntries.Select(e => e.Action).Should().Equal(
            PatientPathwayAuditAction.StageStarted, PatientPathwayAuditAction.StatusChanged);
        change.AuditEntries[1].Details.Should().Contain("\"automatic\":true");
    }

    [Test]
    public void UpdateStageNotes_FirstNotes_RecordsEachEntryATickAfterTheLastSoTheySortInOrder()
    {
        var pathway = CreatePathway(Status.Assigned);
        var change = ChangeOf(pathway);

        change.UpdateStageNotes(pathway.StageAt(1), "Synthetic notes.");

        change.AuditEntries.Select(e => (e.Action, e.AtUtc)).Should().Equal(
            (PatientPathwayAuditAction.StageStarted, Now),
            (PatientPathwayAuditAction.StatusChanged, Now.AddTicks(1)),
            (PatientPathwayAuditAction.StageNotesUpdated, Now.AddTicks(2)));
        // Newest first, as the timeline reads them, whatever their random IDs.
        change.AuditEntries.OrderByDescending(e => e.AtUtc).Select(e => e.Action).Should().Equal(
            PatientPathwayAuditAction.StageNotesUpdated, PatientPathwayAuditAction.StatusChanged, PatientPathwayAuditAction.StageStarted);
        pathway.LastUpdated.Should().Be(Now);
    }

    [Test]
    public void StartStage_AlreadyStarted_ChangesNothing()
    {
        var pathway = CreatePathway(Status.InProgress);
        pathway.StageAt(1).StartedAtUtc = Now.AddDays(-1);
        var change = ChangeOf(pathway);

        change.StartStage(pathway.StageAt(1));

        change.HasChanges.Should().BeFalse();
        pathway.StageAt(1).StartedAtUtc.Should().Be(Now.AddDays(-1));
    }

    [Test]
    public void UpdateStageNotes_SameNotes_ChangesNothing()
    {
        var pathway = CreatePathway(Status.Assigned);
        var change = ChangeOf(pathway);

        change.UpdateStageNotes(pathway.StageAt(1), string.Empty);

        change.HasChanges.Should().BeFalse();
        pathway.StageAt(1).StartedAtUtc.Should().BeNull();
    }

    [Test]
    public void UpdateStageNotes_NewNotes_StartsTheStageAndAuditsTheOldAndNewText()
    {
        var pathway = CreatePathway(Status.Assigned);
        var change = ChangeOf(pathway);

        change.UpdateStageNotes(pathway.StageAt(1), "Patient seen.");

        pathway.StageAt(1).Notes.Should().Be("Patient seen.");
        var notesEntry = change.AuditEntries.Single(e => e.Action == PatientPathwayAuditAction.StageNotesUpdated);
        notesEntry.StageName.Should().Be(ConsultationStage);
        notesEntry.Details.Should().Be("{\"from\":\"\",\"to\":\"Patient seen.\"}");
        change.AuditEntries.Should().Contain(e => e.Action == PatientPathwayAuditAction.StageStarted);
    }

    [Test]
    public void FinishStage_NotTheLastStage_LocksItAndKeepsThePathwayInProgress()
    {
        var pathway = CreatePathway(Status.InProgress);
        var change = ChangeOf(pathway);

        change.FinishStage(pathway.StageAt(1));

        var stage = pathway.StageAt(1);
        stage.IsCompleted.Should().BeTrue();
        stage.IsLocked.Should().BeTrue();
        stage.CompletedAtUtc.Should().Be(Now);
        stage.CompletedByUserId.Should().Be(Staff.UserId);
        pathway.Status.Should().Be(Status.InProgress);
        PathwayWorkflow.CurrentStage(pathway).Should().BeSameAs(pathway.StageAt(2));
    }

    [Test]
    public void FinishStage_TheLastStage_MovesThePathwayToInReview()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 2);
        var change = ChangeOf(pathway);

        change.FinishStage(pathway.StageAt(3));

        pathway.Status.Should().Be(Status.InReview);
        change.AuditEntries.Last().Details.Should().Contain("\"to\":\"InReview\"");
    }

    [Test]
    public void UnlockStage_InReview_UnlocksJustThatStageAndMovesBackToInProgress()
    {
        var pathway = CreatePathway(Status.InReview, finishedStages: 3);
        var change = ChangeOf(pathway, Admin);

        change.UnlockStage(pathway.StageAt(2));

        pathway.StageAt(2).IsLocked.Should().BeFalse();
        pathway.StageAt(2).IsCompleted.Should().BeTrue();
        pathway.StageAt(1).IsLocked.Should().BeTrue();
        pathway.Status.Should().Be(Status.InProgress);
        change.AuditEntries[0].Action.Should().Be(PatientPathwayAuditAction.StageUnlocked);
        change.AuditEntries[0].Username.Should().Be(Admin.Username);
    }

    [Test]
    public void UnlockStage_Completed_MovesBackToInProgressAutomatically()
    {
        var pathway = CreatePathway(Status.Completed, finishedStages: 3);
        var change = ChangeOf(pathway, Admin);

        change.UnlockStage(pathway.StageAt(3));

        pathway.StageAt(3).IsLocked.Should().BeFalse();
        pathway.Status.Should().Be(Status.InProgress);
        change.AuditEntries.Select(e => e.Action).Should().Equal(
            PatientPathwayAuditAction.StageUnlocked, PatientPathwayAuditAction.StatusChanged);
        change.AuditEntries[1].Details.Should().Contain("\"from\":\"Completed\"").And.Contain("\"automatic\":true");
    }

    [Test]
    public void UnlockStage_InProgress_UnlocksTheStageAndKeepsTheStatus()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 2);
        var change = ChangeOf(pathway, Admin);

        change.UnlockStage(pathway.StageAt(1));

        pathway.StageAt(1).IsLocked.Should().BeFalse();
        pathway.StageAt(1).LastUpdated.Should().Be(Now);
        pathway.Status.Should().Be(Status.InProgress);
        change.AuditEntries.Should().ContainSingle().Which.Action.Should().Be(PatientPathwayAuditAction.StageUnlocked);
    }

    [Test]
    public void FinishStage_ReFinishingAnUnlockedStage_GoesBackToInReview()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 3);
        pathway.StageAt(2).IsLocked = false;

        ChangeOf(pathway).FinishStage(pathway.StageAt(2));

        pathway.Status.Should().Be(Status.InReview);
    }

    [Test]
    public void Assign_UnassignedPathway_AddsTheAssignmentAndMovesToAssigned()
    {
        var pathway = CreatePathway(Status.Unassigned);
        var change = ChangeOf(pathway, Admin);

        change.Assign(OtherUserId, OtherUsername);

        pathway.Assignments.Should().ContainSingle(a => a.UserId == OtherUserId && a.AssignedByUserId == Admin.UserId);
        pathway.Status.Should().Be(Status.Assigned);
        change.AuditEntries[0].Details.Should().Contain(OtherUsername);
    }

    [Test]
    public void Unassign_LastAssignee_MovesBackToUnassigned()
    {
        var pathway = CreatePathway(Status.Assigned);

        ChangeOf(pathway, Admin).Unassign(pathway.Assignments[0], Staff.Username);

        pathway.Assignments.Should().BeEmpty();
        pathway.Status.Should().Be(Status.Unassigned);
    }

    [Test]
    public void Unassign_PathwayInProgress_KeepsItsStatus()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 1);

        ChangeOf(pathway, Admin).Unassign(pathway.Assignments[0], Staff.Username);

        pathway.Status.Should().Be(Status.InProgress);
    }

    [Test]
    public void Assign_PathwayBlockedWhileUnassigned_UnblocksToAssigned()
    {
        var pathway = CreatePathway(Status.Blocked);
        pathway.Assignments.Clear();
        pathway.BlockedFromStatus = Status.Unassigned;

        ChangeOf(pathway, Admin).Assign(OtherUserId, OtherUsername);

        pathway.Status.Should().Be(Status.Blocked);
        pathway.BlockedFromStatus.Should().Be(Status.Assigned);
    }

    [Test]
    public void ChangeStatus_ToBlocked_RemembersTheStatusAndRecordsTheReason()
    {
        var pathway = CreatePathway(Status.InProgress);
        var change = ChangeOf(pathway);

        change.ChangeStatus(Status.Blocked, Reason);

        pathway.Status.Should().Be(Status.Blocked);
        pathway.BlockedFromStatus.Should().Be(Status.InProgress);
        change.AuditEntries[0].Details.Should().Contain(Reason).And.Contain("\"automatic\":false");
    }

    [Test]
    public void ChangeStatus_Unblocking_ClearsTheBlockedFromStatus()
    {
        var pathway = CreatePathway(Status.Blocked);
        pathway.BlockedFromStatus = Status.InProgress;

        ChangeOf(pathway).ChangeStatus(Status.InProgress, null);

        pathway.Status.Should().Be(Status.InProgress);
        pathway.BlockedFromStatus.Should().BeNull();
    }

    [Test]
    public void ChangeStatus_ToTheCurrentStatus_ChangesNothingAndAuditsNothing()
    {
        var pathway = CreatePathway(Status.InProgress);
        var change = ChangeOf(pathway);

        change.ChangeStatus(Status.InProgress, Reason);

        pathway.Status.Should().Be(Status.InProgress);
        pathway.LastUpdated.Should().Be(Now.AddDays(-7));
        change.HasChanges.Should().BeFalse();
        change.AuditEntries.Should().BeEmpty();
    }

    [Test]
    public void Properties_NewChange_ExposeThePathwayAndTime()
    {
        var pathway = CreatePathway();

        var change = ChangeOf(pathway);

        change.Pathway.Should().BeSameAs(pathway);
        change.NowUtc.Should().Be(Now);
        change.HasChanges.Should().BeFalse();
    }

    [Test]
    public void Audit_AnyChange_RecordsThePathwayActorAndTime()
    {
        var pathway = CreatePathway();
        var change = ChangeOf(pathway);

        change.Audit(PatientPathwayAuditAction.DetailsUpdated);

        var entry = change.AuditEntries.Single();
        entry.PatientPathwayId.Should().Be(pathway.Id);
        entry.PathwayTitle.Should().Be(PathwayTitle);
        entry.PatientId.Should().Be(pathway.PatientId);
        entry.UserId.Should().Be(Staff.UserId);
        entry.Username.Should().Be(Staff.Username);
        entry.AtUtc.Should().Be(Now);
        entry.Details.Should().Be("{}");
    }

    private static Appointment RequestedFor(PatientPathway pathway, PatientPathwayStage stage) => new()
    {
        Id = Guid.NewGuid(),
        PatientId = pathway.PatientId,
        PathwayId = pathway.Id,
        StageId = stage.Id,
        Status = AppointmentStatus.Requested,
        IsAutomatic = true,
        CreatedAtUtc = Now.AddDays(-1),
        CreatedByUserId = Staff.UserId,
        LastUpdated = Now.AddDays(-1)
    };

    [Test]
    public void RequestVisit_Stage_AsksForAnAutomaticRequestedAppointmentWithItsHistory()
    {
        var pathway = CreatePathway(Status.Assigned);
        var stage = pathway.StageAt(1);
        var change = ChangeOf(pathway);

        var appointment = change.RequestVisit(stage);

        change.NewAppointments.Should().ContainSingle().Which.Should().BeSameAs(appointment);
        appointment.Should().Match<Appointment>(a =>
            a.PatientId == pathway.PatientId
            && a.PathwayId == pathway.Id
            && a.StageId == stage.Id
            && a.Status == AppointmentStatus.Requested
            && a.IsAutomatic
            && a.StaffUserId == null
            && a.StartUtc == null
            && a.CreatedAtUtc == Now
            && a.CreatedByUserId == Staff.UserId);
        var entry = change.AppointmentEvents.Should().ContainSingle().Subject;
        entry.AppointmentId.Should().Be(appointment.Id);
        entry.Action.Should().Be(AppointmentEventAction.Requested);
        entry.PathwayTitle.Should().Be(PathwayTitle);
        entry.StageName.Should().Be(ConsultationStage);
        entry.Details.Should().Be("{\"automatic\":true}");
        change.HasChanges.Should().BeTrue();
        change.AuditEntries.Should().BeEmpty();
    }

    [Test]
    public void CancelRequestedVisit_Requested_CancelsItAutomaticallyWithTheReason()
    {
        var pathway = CreatePathway(Status.InProgress);
        var appointment = RequestedFor(pathway, pathway.StageAt(1));
        var change = ChangeOf(pathway);

        change.CancelRequestedVisit(appointment, PathwayChange.StageFinishedReason);

        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
        appointment.CancelReason.Should().Be("The stage was finished.");
        appointment.LastUpdated.Should().Be(Now);
        var entry = change.AppointmentEvents.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AppointmentEventAction.Cancelled);
        entry.StageName.Should().Be(ConsultationStage);
        var details = JsonDocument.Parse(entry.Details).RootElement;
        details.GetProperty("reason").GetString().Should().Be("The stage was finished.");
        details.GetProperty("automatic").GetBoolean().Should().BeTrue();
    }

    [TestCase(AppointmentStatus.Booked)]
    [TestCase(AppointmentStatus.Attended)]
    [TestCase(AppointmentStatus.Cancelled)]
    public void CancelRequestedVisit_NotRequested_LeavesItAlone(AppointmentStatus status)
    {
        var pathway = CreatePathway(Status.InProgress);
        var appointment = RequestedFor(pathway, pathway.StageAt(1));
        appointment.Status = status;
        var change = ChangeOf(pathway);

        change.CancelRequestedVisit(appointment, PathwayChange.PathwayCancelledReason);

        appointment.Status.Should().Be(status);
        change.HasChanges.Should().BeFalse();
    }

    [Test]
    public void FinishStage_WithoutItsVisit_RecordsTheRequirementAndReason()
    {
        var pathway = CreatePathway(Status.InProgress);
        var stage = pathway.StageAt(1);
        stage.VisitRequirement = VisitRequirement.Required;
        var change = ChangeOf(pathway, Admin);

        change.FinishStage(stage, new FinishedWithoutVisit(VisitRequirement.Required, Reason));

        var finished = change.AuditEntries.Single(e => e.Action == PatientPathwayAuditAction.StageFinished);
        finished.Details.Should().Be($"{{\"withoutVisit\":true,\"visitRequirement\":\"Required\",\"reason\":\"{Reason}\"}}");
    }

    [Test]
    public void FinishStage_SuggestedVisitWithoutAReason_LeavesTheReasonOut()
    {
        var pathway = CreatePathway(Status.InProgress);
        var change = ChangeOf(pathway);

        change.FinishStage(pathway.StageAt(1), new FinishedWithoutVisit(VisitRequirement.Suggested, null));

        change.AuditEntries.Single(e => e.Action == PatientPathwayAuditAction.StageFinished)
            .Details.Should().Be("{\"withoutVisit\":true,\"visitRequirement\":\"Suggested\"}");
    }

    [Test]
    public void FinishStage_WithItsVisit_HasNoDetails()
    {
        var pathway = CreatePathway(Status.InProgress);
        var change = ChangeOf(pathway);

        change.FinishStage(pathway.StageAt(1));

        change.AuditEntries.Single(e => e.Action == PatientPathwayAuditAction.StageFinished).Details.Should().Be("{}");
    }

    [Test]
    public void Entries_AuditAndAppointmentHistory_ShareOneTickOrder()
    {
        var pathway = CreatePathway(Status.InProgress);
        var first = pathway.StageAt(1);
        var appointment = RequestedFor(pathway, first);
        var change = ChangeOf(pathway);

        change.FinishStage(first);
        change.CancelRequestedVisit(appointment, PathwayChange.StageFinishedReason);
        change.RequestVisit(pathway.StageAt(2));

        change.AuditEntries.Select(e => e.AtUtc).Concat(change.AppointmentEvents.Select(e => e.AtUtc))
            .Should().OnlyHaveUniqueItems();
        change.AppointmentEvents.Select(e => e.AtUtc).Should().BeInAscendingOrder();
        change.AppointmentEvents[0].AtUtc.Should().BeAfter(change.AuditEntries[^1].AtUtc);
    }

    [Test]
    public void StageVisits_CurrentStageWithAVisit_AsksForIt()
    {
        var pathway = CreatePathway(Status.Assigned);
        pathway.StageAt(1).VisitRequirement = VisitRequirement.Suggested;
        var change = ChangeOf(pathway);

        var requested = StageVisits.RequestForCurrentStage(change, []);

        requested.Should().NotBeNull();
        requested!.StageId.Should().Be(pathway.StageAt(1).Id);
        change.NewAppointments.Should().ContainSingle();
    }

    [Test]
    public void StageVisits_CurrentStageWithoutAVisit_AsksForNothing()
    {
        var change = ChangeOf(CreatePathway(Status.Assigned));

        StageVisits.RequestForCurrentStage(change, []).Should().BeNull();
        change.HasChanges.Should().BeFalse();
    }

    [TestCase(AppointmentStatus.Requested, false)]
    [TestCase(AppointmentStatus.Booked, false)]
    [TestCase(AppointmentStatus.Attended, false)]
    [TestCase(AppointmentStatus.DidNotAttend, true)]
    [TestCase(AppointmentStatus.Cancelled, true)]
    public void StageVisits_StageAlreadyHasAnAppointment_AsksOnlyIfItIsntOpenOrAttended(AppointmentStatus status, bool asks)
    {
        var pathway = CreatePathway(Status.Assigned);
        pathway.StageAt(1).VisitRequirement = VisitRequirement.Required;
        var existing = RequestedFor(pathway, pathway.StageAt(1));
        existing.Status = status;
        var change = ChangeOf(pathway);

        var requested = StageVisits.RequestForCurrentStage(change, [existing]);

        (requested is not null).Should().Be(asks);
    }

    [Test]
    public void StageVisits_CalledTwice_AsksOnce()
    {
        var pathway = CreatePathway(Status.Assigned);
        pathway.StageAt(1).VisitRequirement = VisitRequirement.Required;
        var change = ChangeOf(pathway);

        StageVisits.RequestForCurrentStage(change, []);
        StageVisits.RequestForCurrentStage(change, []);

        change.NewAppointments.Should().ContainSingle();
    }

    [TestCase(Status.Blocked)]
    [TestCase(Status.Cancelled)]
    [TestCase(Status.Completed)]
    public void StageVisits_PathwayBlockedOrClosed_AsksForNothing(Status status)
    {
        var pathway = CreatePathway(status);
        pathway.StageAt(1).VisitRequirement = VisitRequirement.Required;

        StageVisits.RequestForCurrentStage(ChangeOf(pathway), []).Should().BeNull();
    }
}
