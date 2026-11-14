using FluentAssertions;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

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
}
