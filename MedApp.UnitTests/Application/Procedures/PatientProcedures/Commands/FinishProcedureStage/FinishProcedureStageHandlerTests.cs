using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;
using MedApp.Application.Procedures.PatientProcedures.Commands.FinishProcedureStage;

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
}
