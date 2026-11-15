using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;
using MedApp.Application.Procedures.PatientProcedures.Commands.ChangePatientProcedureStatus;

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
}
