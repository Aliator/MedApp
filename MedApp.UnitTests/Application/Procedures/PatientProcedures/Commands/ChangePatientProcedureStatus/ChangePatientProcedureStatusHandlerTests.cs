using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;
using MedApp.Application.Procedures.PatientProcedures.Commands.ChangePatientProcedureStatus;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Appointments;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Commands.ChangePatientProcedureStatus;

[TestFixture]
public sealed class ChangePatientProcedureStatusHandlerTests : ProcedureHandlerTestBase
{
    private ChangePatientProcedureStatusHandler Handler => Fixture.Create<ChangePatientProcedureStatusHandler>();

    [Test]
    public async Task Handle_ChangeNotAllowed_ThrowsConflict()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.Assigned));

        var act = () => Handler.Handle(new ChangePatientProcedureStatusCommand(procedure.Id, "Completed", null, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*assigned*completed*");
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_Block_BlocksWithTheReasonAndSaves()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress, finishedStages: 1));

        var response = await Handler.Handle(
            new ChangePatientProcedureStatusCommand(procedure.Id, "blocked", $"  {Reason} ", Staff), CancellationToken.None);

        response.Status.Should().Be("Blocked");
        response.BlockedFromStatus.Should().Be("InProgress");
        response.AllowedStatusChanges.Should().Equal("InProgress", "Cancelled");
        SavedAudit.Single().Details.Should().Contain($"\"reason\":\"{Reason}\"");
        VerifySavedOnce();
    }

    [Test]
    public async Task Handle_CompleteFromReview_Completes()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InReview, finishedStages: 3));

        var response = await Handler.Handle(new ChangePatientProcedureStatusCommand(procedure.Id, "Completed", null, Staff), CancellationToken.None);

        response.Status.Should().Be("Completed");
        response.AllowedStatusChanges.Should().Equal("Archived");
    }

    [Test]
    public async Task Handle_CompleteWithAStageReopened_ThrowsConflict()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InReview, finishedStages: 3));
        procedure.StageAt(2).IsLocked = false;

        var act = () => Handler.Handle(new ChangePatientProcedureStatusCommand(procedure.Id, "Completed", null, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Handle_Cancel_CancelsTheProceduresRequestedVisitsButNotBookedOnes()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress, finishedStages: 1));
        var requested = CreateStageAppointment(procedure, procedure.StageAt(2));
        var booked = CreateStageAppointment(procedure, procedure.StageAt(3), AppointmentStatus.Booked);
        Appointments
            .Setup(r => r.GetRequestedForProcedureForUpdateAsync(procedure.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([requested, booked]);

        var response = await Handler.Handle(
            new ChangePatientProcedureStatusCommand(procedure.Id, "Cancelled", Reason, Staff), CancellationToken.None);

        response.Status.Should().Be("Cancelled");
        requested.Status.Should().Be(AppointmentStatus.Cancelled);
        requested.CancelReason.Should().Be(ProcedureChange.ProcedureCancelledReason);
        booked.Status.Should().Be(AppointmentStatus.Booked);
        var change = SavedChanges.Should().ContainSingle().Subject;
        change.AppointmentEvents.Should().ContainSingle(e => e.AppointmentId == requested.Id && e.Action == AppointmentEventAction.Cancelled);
        change.AuditEntries.Should().ContainSingle(e => e.Action == PatientProcedureAuditAction.StatusChanged);
    }

    [Test]
    public async Task Handle_Cancel_PassesTheTokenToTheAppointmentsAndTheSave()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        using var cts = new CancellationTokenSource();

        await Handler.Handle(new ChangePatientProcedureStatusCommand(procedure.Id, "Cancelled", null, Staff), cts.Token);

        Appointments.Verify(r => r.GetRequestedForProcedureForUpdateAsync(procedure.Id, cts.Token), Times.Once);
        VerifySavedOnce(cts.Token);
    }

    [Test]
    public async Task Handle_NotCancelling_LeavesTheAppointmentsAlone()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));

        await Handler.Handle(new ChangePatientProcedureStatusCommand(procedure.Id, "Blocked", Reason, Staff), CancellationToken.None);

        Appointments.Verify(
            r => r.GetRequestedForProcedureForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        SavedChanges.Single().AppointmentEvents.Should().BeEmpty();
    }
}
