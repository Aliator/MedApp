using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;
using MedApp.Application.Procedures.PatientProcedures.Commands.StartProcedureStage;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Commands.StartProcedureStage;

[TestFixture]
public sealed class StartProcedureStageHandlerTests : ProcedureHandlerTestBase
{
    private StartProcedureStageHandler Handler => Fixture.Create<StartProcedureStageHandler>();

    [Test]
    public async Task Handle_StageNotOnTheProcedure_ThrowsNotFound()
    {
        var procedure = GivenProcedure(CreateProcedure());

        var act = () => Handler.Handle(new StartProcedureStageCommand(procedure.Id, Guid.NewGuid(), Staff), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_StageAfterTheCurrentOne_ThrowsConflict()
    {
        var procedure = GivenProcedure(CreateProcedure());

        var act = () => Handler.Handle(new StartProcedureStageCommand(procedure.Id, procedure.StageAt(2).Id, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*in order*");
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_StageAlreadyStarted_ThrowsConflict()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        procedure.StageAt(1).StartedAtUtc = Now.AddHours(-1);

        var act = () => Handler.Handle(new StartProcedureStageCommand(procedure.Id, procedure.StageAt(1).Id, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*already been started*");
    }

    [Test]
    public async Task Handle_CurrentStage_StartsItAndSaves()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.Assigned));

        var response = await Handler.Handle(new StartProcedureStageCommand(procedure.Id, procedure.StageAt(1).Id, Staff), CancellationToken.None);

        response.Status.Should().Be("InProgress");
        response.Stages[0].State.Should().Be("InProgress");
        response.Stages[0].StartedByUsername.Should().Be(Staff.Username);
        VerifySavedOnce();
    }
}
