using FluentAssertions;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;
using System.Text.Json;
using MedApp.Domain.Appointments;

namespace MedApp.UnitTests.Application.Procedures.Common;

[TestFixture]
public sealed class ProcedureChangeTests
{
    private static ProcedureChange ChangeOf(PatientProcedure procedure, ProcedureActor? actor = null) =>
        new(procedure, actor ?? Staff, Now);

    [Test]
    public void StartStage_AssignedProcedure_StartsTheStageAndMovesToInProgress()
    {
        var procedure = CreateProcedure(Status.Assigned);
        var change = ChangeOf(procedure);

        change.StartStage(procedure.StageAt(1));

        procedure.StageAt(1).StartedAtUtc.Should().Be(Now);
        procedure.StageAt(1).StartedByUserId.Should().Be(Staff.UserId);
        procedure.Status.Should().Be(Status.InProgress);
        procedure.LastUpdated.Should().Be(Now);
        change.AuditEntries.Select(e => e.Action).Should().Equal(
            PatientProcedureAuditAction.StageStarted, PatientProcedureAuditAction.StatusChanged);
        change.AuditEntries[1].Details.Should().Contain("\"automatic\":true");
    }

    [Test]
    public void UpdateStageNotes_FirstNotes_RecordsEachEntryATickAfterTheLastSoTheySortInOrder()
    {
        var procedure = CreateProcedure(Status.Assigned);
        var change = ChangeOf(procedure);

        change.UpdateStageNotes(procedure.StageAt(1), "Synthetic notes.");

        change.AuditEntries.Select(e => (e.Action, e.AtUtc)).Should().Equal(
            (PatientProcedureAuditAction.StageStarted, Now),
            (PatientProcedureAuditAction.StatusChanged, Now.AddTicks(1)),
            (PatientProcedureAuditAction.StageNotesUpdated, Now.AddTicks(2)));
        // Newest first, as the timeline reads them, whatever their random IDs.
        change.AuditEntries.OrderByDescending(e => e.AtUtc).Select(e => e.Action).Should().Equal(
            PatientProcedureAuditAction.StageNotesUpdated, PatientProcedureAuditAction.StatusChanged, PatientProcedureAuditAction.StageStarted);
        procedure.LastUpdated.Should().Be(Now);
    }

    [Test]
    public void StartStage_AlreadyStarted_ChangesNothing()
    {
        var procedure = CreateProcedure(Status.InProgress);
        procedure.StageAt(1).StartedAtUtc = Now.AddDays(-1);
        var change = ChangeOf(procedure);

        change.StartStage(procedure.StageAt(1));

        change.HasChanges.Should().BeFalse();
        procedure.StageAt(1).StartedAtUtc.Should().Be(Now.AddDays(-1));
    }

    [Test]
    public void UpdateStageNotes_SameNotes_ChangesNothing()
    {
        var procedure = CreateProcedure(Status.Assigned);
        var change = ChangeOf(procedure);

        change.UpdateStageNotes(procedure.StageAt(1), string.Empty);

        change.HasChanges.Should().BeFalse();
        procedure.StageAt(1).StartedAtUtc.Should().BeNull();
    }

    [Test]
    public void UpdateStageNotes_NewNotes_StartsTheStageAndAuditsTheOldAndNewText()
    {
        var procedure = CreateProcedure(Status.Assigned);
        var change = ChangeOf(procedure);

        change.UpdateStageNotes(procedure.StageAt(1), "Patient seen.");

        procedure.StageAt(1).Notes.Should().Be("Patient seen.");
        var notesEntry = change.AuditEntries.Single(e => e.Action == PatientProcedureAuditAction.StageNotesUpdated);
        notesEntry.StageName.Should().Be(ConsultationStage);
        notesEntry.Details.Should().Be("{\"from\":\"\",\"to\":\"Patient seen.\"}");
        change.AuditEntries.Should().Contain(e => e.Action == PatientProcedureAuditAction.StageStarted);
    }

    [Test]
    public void FinishStage_NotTheLastStage_LocksItAndKeepsTheProcedureInProgress()
    {
        var procedure = CreateProcedure(Status.InProgress);
        var change = ChangeOf(procedure);

        change.FinishStage(procedure.StageAt(1));

        var stage = procedure.StageAt(1);
        stage.IsCompleted.Should().BeTrue();
        stage.IsLocked.Should().BeTrue();
        stage.CompletedAtUtc.Should().Be(Now);
        stage.CompletedByUserId.Should().Be(Staff.UserId);
        procedure.Status.Should().Be(Status.InProgress);
        ProcedureWorkflow.CurrentStage(procedure).Should().BeSameAs(procedure.StageAt(2));
    }

    [Test]
    public void FinishStage_TheLastStage_MovesTheProcedureToInReview()
    {
        var procedure = CreateProcedure(Status.InProgress, finishedStages: 2);
        var change = ChangeOf(procedure);

        change.FinishStage(procedure.StageAt(3));

        procedure.Status.Should().Be(Status.InReview);
        change.AuditEntries.Last().Details.Should().Contain("\"to\":\"InReview\"");
    }

    [Test]
    public void UnlockStage_InReview_UnlocksJustThatStageAndMovesBackToInProgress()
    {
        var procedure = CreateProcedure(Status.InReview, finishedStages: 3);
        var change = ChangeOf(procedure, Admin);

        change.UnlockStage(procedure.StageAt(2));

        procedure.StageAt(2).IsLocked.Should().BeFalse();
        procedure.StageAt(2).IsCompleted.Should().BeTrue();
        procedure.StageAt(1).IsLocked.Should().BeTrue();
        procedure.Status.Should().Be(Status.InProgress);
        change.AuditEntries[0].Action.Should().Be(PatientProcedureAuditAction.StageUnlocked);
        change.AuditEntries[0].Username.Should().Be(Admin.Username);
    }

    [Test]
    public void UnlockStage_Completed_MovesBackToInProgressAutomatically()
    {
        var procedure = CreateProcedure(Status.Completed, finishedStages: 3);
        var change = ChangeOf(procedure, Admin);

        change.UnlockStage(procedure.StageAt(3));

        procedure.StageAt(3).IsLocked.Should().BeFalse();
        procedure.Status.Should().Be(Status.InProgress);
        change.AuditEntries.Select(e => e.Action).Should().Equal(
            PatientProcedureAuditAction.StageUnlocked, PatientProcedureAuditAction.StatusChanged);
        change.AuditEntries[1].Details.Should().Contain("\"from\":\"Completed\"").And.Contain("\"automatic\":true");
    }

    [Test]
    public void UnlockStage_InProgress_UnlocksTheStageAndKeepsTheStatus()
    {
        var procedure = CreateProcedure(Status.InProgress, finishedStages: 2);
        var change = ChangeOf(procedure, Admin);

        change.UnlockStage(procedure.StageAt(1));

        procedure.StageAt(1).IsLocked.Should().BeFalse();
        procedure.StageAt(1).LastUpdated.Should().Be(Now);
        procedure.Status.Should().Be(Status.InProgress);
        change.AuditEntries.Should().ContainSingle().Which.Action.Should().Be(PatientProcedureAuditAction.StageUnlocked);
    }

    [Test]
    public void FinishStage_ReFinishingAnUnlockedStage_GoesBackToInReview()
    {
        var procedure = CreateProcedure(Status.InProgress, finishedStages: 3);
        procedure.StageAt(2).IsLocked = false;

        ChangeOf(procedure).FinishStage(procedure.StageAt(2));

        procedure.Status.Should().Be(Status.InReview);
    }

    [Test]
    public void Assign_UnassignedProcedure_AddsTheAssignmentAndMovesToAssigned()
    {
        var procedure = CreateProcedure(Status.Unassigned);
        var change = ChangeOf(procedure, Admin);

        change.Assign(OtherUserId, OtherUsername);

        procedure.Assignments.Should().ContainSingle(a => a.UserId == OtherUserId && a.AssignedByUserId == Admin.UserId);
        procedure.Status.Should().Be(Status.Assigned);
        change.AuditEntries[0].Details.Should().Contain(OtherUsername);
    }

    [Test]
    public void Unassign_LastAssignee_MovesBackToUnassigned()
    {
        var procedure = CreateProcedure(Status.Assigned);

        ChangeOf(procedure, Admin).Unassign(procedure.Assignments[0], Staff.Username);

        procedure.Assignments.Should().BeEmpty();
        procedure.Status.Should().Be(Status.Unassigned);
    }

    [Test]
    public void Unassign_ProcedureInProgress_KeepsItsStatus()
    {
        var procedure = CreateProcedure(Status.InProgress, finishedStages: 1);

        ChangeOf(procedure, Admin).Unassign(procedure.Assignments[0], Staff.Username);

        procedure.Status.Should().Be(Status.InProgress);
    }

    [Test]
    public void Assign_ProcedureBlockedWhileUnassigned_UnblocksToAssigned()
    {
        var procedure = CreateProcedure(Status.Blocked);
        procedure.Assignments.Clear();
        procedure.BlockedFromStatus = Status.Unassigned;

        ChangeOf(procedure, Admin).Assign(OtherUserId, OtherUsername);

        procedure.Status.Should().Be(Status.Blocked);
        procedure.BlockedFromStatus.Should().Be(Status.Assigned);
    }

    [Test]
    public void ChangeStatus_ToBlocked_RemembersTheStatusAndRecordsTheReason()
    {
        var procedure = CreateProcedure(Status.InProgress);
        var change = ChangeOf(procedure);

        change.ChangeStatus(Status.Blocked, Reason);

        procedure.Status.Should().Be(Status.Blocked);
        procedure.BlockedFromStatus.Should().Be(Status.InProgress);
        change.AuditEntries[0].Details.Should().Contain(Reason).And.Contain("\"automatic\":false");
    }

    [Test]
    public void ChangeStatus_Unblocking_ClearsTheBlockedFromStatus()
    {
        var procedure = CreateProcedure(Status.Blocked);
        procedure.BlockedFromStatus = Status.InProgress;

        ChangeOf(procedure).ChangeStatus(Status.InProgress, null);

        procedure.Status.Should().Be(Status.InProgress);
        procedure.BlockedFromStatus.Should().BeNull();
    }

    [Test]
    public void ChangeStatus_ToTheCurrentStatus_ChangesNothingAndAuditsNothing()
    {
        var procedure = CreateProcedure(Status.InProgress);
        var change = ChangeOf(procedure);

        change.ChangeStatus(Status.InProgress, Reason);

        procedure.Status.Should().Be(Status.InProgress);
        procedure.LastUpdated.Should().Be(Now.AddDays(-7));
        change.HasChanges.Should().BeFalse();
        change.AuditEntries.Should().BeEmpty();
    }

    [Test]
    public void Properties_NewChange_ExposeTheProcedureAndTime()
    {
        var procedure = CreateProcedure();

        var change = ChangeOf(procedure);

        change.Procedure.Should().BeSameAs(procedure);
        change.NowUtc.Should().Be(Now);
        change.HasChanges.Should().BeFalse();
    }

    [Test]
    public void Audit_AnyChange_RecordsTheProcedureActorAndTime()
    {
        var procedure = CreateProcedure();
        var change = ChangeOf(procedure);

        change.Audit(PatientProcedureAuditAction.DetailsUpdated);

        var entry = change.AuditEntries.Single();
        entry.PatientProcedureId.Should().Be(procedure.Id);
        entry.ProcedureTitle.Should().Be(ProcedureTitle);
        entry.PatientId.Should().Be(procedure.PatientId);
        entry.UserId.Should().Be(Staff.UserId);
        entry.Username.Should().Be(Staff.Username);
        entry.AtUtc.Should().Be(Now);
        entry.Details.Should().Be("{}");
    }

    private static Appointment RequestedFor(PatientProcedure procedure, PatientProcedureStage stage) => new()
    {
        Id = Guid.NewGuid(),
        PatientId = procedure.PatientId,
        ProcedureId = procedure.Id,
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
        var procedure = CreateProcedure(Status.Assigned);
        var stage = procedure.StageAt(1);
        var change = ChangeOf(procedure);

        var appointment = change.RequestVisit(stage);

        change.NewAppointments.Should().ContainSingle().Which.Should().BeSameAs(appointment);
        appointment.Should().Match<Appointment>(a =>
            a.PatientId == procedure.PatientId
            && a.ProcedureId == procedure.Id
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
        entry.ProcedureTitle.Should().Be(ProcedureTitle);
        entry.StageName.Should().Be(ConsultationStage);
        entry.Details.Should().Be("{\"automatic\":true}");
        change.HasChanges.Should().BeTrue();
        change.AuditEntries.Should().BeEmpty();
    }

    [Test]
    public void CancelRequestedVisit_Requested_CancelsItAutomaticallyWithTheReason()
    {
        var procedure = CreateProcedure(Status.InProgress);
        var appointment = RequestedFor(procedure, procedure.StageAt(1));
        var change = ChangeOf(procedure);

        change.CancelRequestedVisit(appointment, ProcedureChange.StageFinishedReason);

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
        var procedure = CreateProcedure(Status.InProgress);
        var appointment = RequestedFor(procedure, procedure.StageAt(1));
        appointment.Status = status;
        var change = ChangeOf(procedure);

        change.CancelRequestedVisit(appointment, ProcedureChange.ProcedureCancelledReason);

        appointment.Status.Should().Be(status);
        change.HasChanges.Should().BeFalse();
    }

    [Test]
    public void FinishStage_WithoutItsVisit_RecordsTheRequirementAndReason()
    {
        var procedure = CreateProcedure(Status.InProgress);
        var stage = procedure.StageAt(1);
        stage.VisitRequirement = VisitRequirement.Required;
        var change = ChangeOf(procedure, Admin);

        change.FinishStage(stage, new FinishedWithoutVisit(VisitRequirement.Required, Reason));

        var finished = change.AuditEntries.Single(e => e.Action == PatientProcedureAuditAction.StageFinished);
        finished.Details.Should().Be($"{{\"withoutVisit\":true,\"visitRequirement\":\"Required\",\"reason\":\"{Reason}\"}}");
    }

    [Test]
    public void FinishStage_SuggestedVisitWithoutAReason_LeavesTheReasonOut()
    {
        var procedure = CreateProcedure(Status.InProgress);
        var change = ChangeOf(procedure);

        change.FinishStage(procedure.StageAt(1), new FinishedWithoutVisit(VisitRequirement.Suggested, null));

        change.AuditEntries.Single(e => e.Action == PatientProcedureAuditAction.StageFinished)
            .Details.Should().Be("{\"withoutVisit\":true,\"visitRequirement\":\"Suggested\"}");
    }

    [Test]
    public void FinishStage_WithItsVisit_HasNoDetails()
    {
        var procedure = CreateProcedure(Status.InProgress);
        var change = ChangeOf(procedure);

        change.FinishStage(procedure.StageAt(1));

        change.AuditEntries.Single(e => e.Action == PatientProcedureAuditAction.StageFinished).Details.Should().Be("{}");
    }

    [Test]
    public void Entries_AuditAndAppointmentHistory_ShareOneTickOrder()
    {
        var procedure = CreateProcedure(Status.InProgress);
        var first = procedure.StageAt(1);
        var appointment = RequestedFor(procedure, first);
        var change = ChangeOf(procedure);

        change.FinishStage(first);
        change.CancelRequestedVisit(appointment, ProcedureChange.StageFinishedReason);
        change.RequestVisit(procedure.StageAt(2));

        change.AuditEntries.Select(e => e.AtUtc).Concat(change.AppointmentEvents.Select(e => e.AtUtc))
            .Should().OnlyHaveUniqueItems();
        change.AppointmentEvents.Select(e => e.AtUtc).Should().BeInAscendingOrder();
        change.AppointmentEvents[0].AtUtc.Should().BeAfter(change.AuditEntries[^1].AtUtc);
    }

    [Test]
    public void StageVisits_CurrentStageWithAVisit_AsksForIt()
    {
        var procedure = CreateProcedure(Status.Assigned);
        procedure.StageAt(1).VisitRequirement = VisitRequirement.Suggested;
        var change = ChangeOf(procedure);

        var requested = StageVisits.RequestForCurrentStage(change, []);

        requested.Should().NotBeNull();
        requested!.StageId.Should().Be(procedure.StageAt(1).Id);
        change.NewAppointments.Should().ContainSingle();
    }

    [Test]
    public void StageVisits_CurrentStageWithoutAVisit_AsksForNothing()
    {
        var change = ChangeOf(CreateProcedure(Status.Assigned));

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
        var procedure = CreateProcedure(Status.Assigned);
        procedure.StageAt(1).VisitRequirement = VisitRequirement.Required;
        var existing = RequestedFor(procedure, procedure.StageAt(1));
        existing.Status = status;
        var change = ChangeOf(procedure);

        var requested = StageVisits.RequestForCurrentStage(change, [existing]);

        (requested is not null).Should().Be(asks);
    }

    [Test]
    public void StageVisits_CalledTwice_AsksOnce()
    {
        var procedure = CreateProcedure(Status.Assigned);
        procedure.StageAt(1).VisitRequirement = VisitRequirement.Required;
        var change = ChangeOf(procedure);

        StageVisits.RequestForCurrentStage(change, []);
        StageVisits.RequestForCurrentStage(change, []);

        change.NewAppointments.Should().ContainSingle();
    }

    [TestCase(Status.Blocked)]
    [TestCase(Status.Cancelled)]
    [TestCase(Status.Completed)]
    public void StageVisits_ProcedureBlockedOrClosed_AsksForNothing(Status status)
    {
        var procedure = CreateProcedure(status);
        procedure.StageAt(1).VisitRequirement = VisitRequirement.Required;

        StageVisits.RequestForCurrentStage(ChangeOf(procedure), []).Should().BeNull();
    }
}
