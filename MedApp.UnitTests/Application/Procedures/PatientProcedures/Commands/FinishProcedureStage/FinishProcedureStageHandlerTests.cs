using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;
using MedApp.Application.Procedures.PatientProcedures.Commands.FinishProcedureStage;
using System.Text.Json;
using FluentValidation;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Appointments;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Commands.FinishProcedureStage;

[TestFixture]
public sealed class FinishProcedureStageHandlerTests : ProcedureHandlerTestBase
{
    private FinishProcedureStageHandler Handler => Fixture.Create<FinishProcedureStageHandler>();

    [Test]
    public async Task Handle_ProcedureBlocked_ThrowsConflict()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.Blocked));

        var act = () => Handler.Handle(new FinishProcedureStageCommand(procedure.Id, procedure.StageAt(1).Id, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*blocked*");
    }

    [Test]
    public async Task Handle_CurrentStage_FinishesAndLocksItAndMovesOn()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.Assigned));

        var response = await Handler.Handle(new FinishProcedureStageCommand(procedure.Id, procedure.StageAt(1).Id, Staff), CancellationToken.None);

        response.Stages[0].State.Should().Be("Completed");
        response.Stages[0].IsLocked.Should().BeTrue();
        response.Stages[0].CompletedByUsername.Should().Be(Staff.Username);
        response.CurrentStageId.Should().Be(procedure.StageAt(2).Id);
        response.Status.Should().Be("InProgress");
        VerifySavedOnce();
    }

    [Test]
    public async Task Handle_LastStage_MovesTheProcedureToReview()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress, finishedStages: 2));

        var response = await Handler.Handle(new FinishProcedureStageCommand(procedure.Id, procedure.StageAt(3).Id, Staff), CancellationToken.None);

        response.Status.Should().Be("InReview");
        response.CurrentStageId.Should().BeNull();
        response.AllowedStatusChanges.Should().Contain("Completed");
    }

    private PatientProcedureAuditEntry FinishedEntry() =>
        SavedAudit.Single(e => e.Action == PatientProcedureAuditAction.StageFinished);

    /// <summary>An InProgress procedure whose first stage involves a visit as given.</summary>
    private PatientProcedure GivenFirstStageWithVisit(VisitRequirement requirement)
    {
        var procedure = CreateProcedure(Status.InProgress);
        procedure.StageAt(1).VisitRequirement = requirement;
        return GivenProcedure(procedure);
    }

    [Test]
    public async Task Handle_RequiredVisitNotAttendedByStaff_ThrowsConflictWithoutSaving()
    {
        var procedure = GivenFirstStageWithVisit(VisitRequirement.Required);

        var act = () => Handler.Handle(new FinishProcedureStageCommand(procedure.Id, procedure.StageAt(1).Id, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"The '{ConsultationStage}' stage needs a visit before it can be finished*An admin can finish it without one.");
        VerifyNotSaved();
        procedure.StageAt(1).IsCompleted.Should().BeFalse();
    }

    [Test]
    public async Task Handle_RequiredVisitStaffAskingToFinishWithoutIt_ThrowsConflict()
    {
        var procedure = GivenFirstStageWithVisit(VisitRequirement.Required);

        var act = () => Handler.Handle(
            new FinishProcedureStageCommand(procedure.Id, procedure.StageAt(1).Id, Staff, WithoutVisit: true, Reason: WithoutVisitReason),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*needs a visit*");
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_RequiredVisitAdminWithoutSayingWithoutVisit_ThrowsConflict()
    {
        var procedure = GivenFirstStageWithVisit(VisitRequirement.Required);

        var act = () => Handler.Handle(
            new FinishProcedureStageCommand(procedure.Id, procedure.StageAt(1).Id, Admin, IsAdmin: true),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*needs a visit*");
        VerifyNotSaved();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task Handle_RequiredVisitAdminWithoutVisitButNoReason_ThrowsValidationException(string? reason)
    {
        var procedure = GivenFirstStageWithVisit(VisitRequirement.Required);

        var act = () => Handler.Handle(
            new FinishProcedureStageCommand(procedure.Id, procedure.StageAt(1).Id, Admin, WithoutVisit: true, Reason: reason, IsAdmin: true),
            CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<ValidationException>();
        thrown.Which.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(FinishProcedureStageCommand.Reason)
            && e.ErrorMessage == FinishProcedureStageHandler.ReasonRequired);
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_RequiredVisitAdminWithoutVisitAndAReason_FinishesAndRecordsTheBypass()
    {
        var procedure = GivenFirstStageWithVisit(VisitRequirement.Required);

        var response = await Handler.Handle(
            new FinishProcedureStageCommand(procedure.Id, procedure.StageAt(1).Id, Admin, WithoutVisit: true, Reason: $"  {WithoutVisitReason} ", IsAdmin: true),
            CancellationToken.None);

        response.Stages[0].State.Should().Be("Completed");
        var details = JsonDocument.Parse(FinishedEntry().Details).RootElement;
        details.GetProperty("withoutVisit").GetBoolean().Should().BeTrue();
        details.GetProperty("visitRequirement").GetString().Should().Be("Required");
        details.GetProperty("reason").GetString().Should().Be(WithoutVisitReason);
        VerifySavedOnce();
    }

    [Test]
    public async Task Handle_RequiredVisitAttended_StaffFinishesWithNothingRecordedAboutTheVisit()
    {
        var procedure = GivenFirstStageWithVisit(VisitRequirement.Required);
        GivenStageAppointments(CreateStageAppointment(procedure, procedure.StageAt(1), AppointmentStatus.Attended));

        var response = await Handler.Handle(new FinishProcedureStageCommand(procedure.Id, procedure.StageAt(1).Id, Staff), CancellationToken.None);

        response.Stages[0].State.Should().Be("Completed");
        FinishedEntry().Details.Should().Be("{}");
    }

    [TestCase(AppointmentStatus.Booked)]
    [TestCase(AppointmentStatus.DidNotAttend)]
    [TestCase(AppointmentStatus.Cancelled)]
    public async Task Handle_RequiredVisitNotAttended_ThrowsConflictWhateverElseItHas(AppointmentStatus status)
    {
        var procedure = GivenFirstStageWithVisit(VisitRequirement.Required);
        GivenStageAppointments(CreateStageAppointment(procedure, procedure.StageAt(1), status));

        var act = () => Handler.Handle(new FinishProcedureStageCommand(procedure.Id, procedure.StageAt(1).Id, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Handle_SuggestedVisitNotAttended_StaffFinishesAndItIsRecorded()
    {
        var procedure = GivenFirstStageWithVisit(VisitRequirement.Suggested);

        await Handler.Handle(new FinishProcedureStageCommand(procedure.Id, procedure.StageAt(1).Id, Staff), CancellationToken.None);

        FinishedEntry().Details.Should().Be("{\"withoutVisit\":true,\"visitRequirement\":\"Suggested\"}");
        VerifySavedOnce();
    }

    [Test]
    public async Task Handle_SuggestedVisitWithAReason_KeepsTheReason()
    {
        var procedure = GivenFirstStageWithVisit(VisitRequirement.Suggested);

        await Handler.Handle(
            new FinishProcedureStageCommand(procedure.Id, procedure.StageAt(1).Id, Staff, WithoutVisit: true, Reason: WithoutVisitReason),
            CancellationToken.None);

        JsonDocument.Parse(FinishedEntry().Details).RootElement.GetProperty("reason").GetString().Should().Be(WithoutVisitReason);
    }

    [Test]
    public async Task Handle_NoVisit_FinishesAsBeforeWithoutTouchingAppointments()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));

        await Handler.Handle(new FinishProcedureStageCommand(procedure.Id, procedure.StageAt(1).Id, Staff), CancellationToken.None);

        FinishedEntry().Details.Should().Be("{}");
        SavedChanges.Single().NewAppointments.Should().BeEmpty();
        SavedChanges.Single().AppointmentEvents.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_RequiredStageFinishedAgainAfterAnUnlock_DoesNotAskForTheVisitAgain()
    {
        var procedure = CreateProcedure(Status.InProgress, finishedStages: 3);
        var stage = procedure.StageAt(2);
        stage.VisitRequirement = VisitRequirement.Required;
        stage.IsLocked = false;
        GivenProcedure(procedure);

        var response = await Handler.Handle(new FinishProcedureStageCommand(procedure.Id, stage.Id, Staff), CancellationToken.None);

        response.Status.Should().Be("InReview");
        FinishedEntry().Details.Should().Be("{}");
    }

    [Test]
    public async Task Handle_StageWithARequestedVisit_CancelsItButLeavesBookedOnes()
    {
        var procedure = GivenFirstStageWithVisit(VisitRequirement.Suggested);
        var requested = CreateStageAppointment(procedure, procedure.StageAt(1));
        var booked = CreateStageAppointment(procedure, procedure.StageAt(1), AppointmentStatus.Booked);
        GivenStageAppointments(requested, booked);

        await Handler.Handle(new FinishProcedureStageCommand(procedure.Id, procedure.StageAt(1).Id, Staff), CancellationToken.None);

        requested.Status.Should().Be(AppointmentStatus.Cancelled);
        requested.CancelReason.Should().Be(ProcedureChange.StageFinishedReason);
        booked.Status.Should().Be(AppointmentStatus.Booked);
        var cancelled = SavedChanges.Single().AppointmentEvents.Should().ContainSingle().Subject;
        cancelled.AppointmentId.Should().Be(requested.Id);
        cancelled.Action.Should().Be(AppointmentEventAction.Cancelled);
    }

    [Test]
    public async Task Handle_NextStageInvolvesAVisit_AsksForIt()
    {
        var procedure = CreateProcedure(Status.InProgress);
        procedure.StageAt(2).VisitRequirement = VisitRequirement.Required;
        GivenProcedure(procedure);

        await Handler.Handle(new FinishProcedureStageCommand(procedure.Id, procedure.StageAt(1).Id, Staff), CancellationToken.None);

        var visit = SavedChanges.Single().NewAppointments.Should().ContainSingle().Subject;
        visit.StageId.Should().Be(procedure.StageAt(2).Id);
        visit.ProcedureId.Should().Be(procedure.Id);
        visit.Status.Should().Be(AppointmentStatus.Requested);
        visit.IsAutomatic.Should().BeTrue();
        SavedChanges.Single().AppointmentEvents.Should().ContainSingle(e => e.Action == AppointmentEventAction.Requested);
    }

    [Test]
    public async Task Handle_NextStageAlreadyBookedAhead_AsksForNothingMore()
    {
        var procedure = CreateProcedure(Status.InProgress);
        procedure.StageAt(2).VisitRequirement = VisitRequirement.Required;
        GivenProcedure(procedure);
        GivenStageAppointments(CreateStageAppointment(procedure, procedure.StageAt(2), AppointmentStatus.Booked));

        await Handler.Handle(new FinishProcedureStageCommand(procedure.Id, procedure.StageAt(1).Id, Staff), CancellationToken.None);

        SavedChanges.Single().NewAppointments.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_NotTheLastStage_LoadsTheAppointmentsOfItAndTheNextStageAndPassesTheToken()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        using var cts = new CancellationTokenSource();

        await Handler.Handle(new FinishProcedureStageCommand(procedure.Id, procedure.StageAt(1).Id, Staff), cts.Token);

        Appointments.Verify(r => r.GetForStagesForUpdateAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { procedure.StageAt(1).Id, procedure.StageAt(2).Id })),
            cts.Token), Times.Once);
        VerifySavedOnce(cts.Token);
    }

    [Test]
    public async Task Handle_TheLastStage_LoadsOnlyItsOwnAppointments()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress, finishedStages: 2));

        await Handler.Handle(new FinishProcedureStageCommand(procedure.Id, procedure.StageAt(3).Id, Staff), CancellationToken.None);

        Appointments.Verify(r => r.GetForStagesForUpdateAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { procedure.StageAt(3).Id })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_StageAlreadyFinishedAndLocked_ThrowsConflictWithoutSaving()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress, finishedStages: 1));

        var act = () => Handler.Handle(new FinishProcedureStageCommand(procedure.Id, procedure.StageAt(1).Id, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        VerifyNotSaved();
    }
}
