using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;
using MedApp.Application.Procedures.PatientProcedures.Commands.UnlockProcedureStage;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Commands.UnlockProcedureStage;

[TestFixture]
public sealed class UnlockProcedureStageHandlerTests : ProcedureHandlerTestBase
{
    private UnlockProcedureStageHandler Handler => Fixture.Create<UnlockProcedureStageHandler>();

    [Test]
    public async Task Handle_StageNotFinished_ThrowsConflict()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress, finishedStages: 1));

        var act = () => Handler.Handle(new UnlockProcedureStageCommand(procedure.Id, procedure.StageAt(2).Id, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*nothing to unlock*");
    }

    [Test]
    public async Task Handle_ProcedureCancelled_ThrowsConflict()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.Cancelled, finishedStages: 1));

        var act = () => Handler.Handle(new UnlockProcedureStageCommand(procedure.Id, procedure.StageAt(1).Id, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*cancelled*");
    }

    [Test]
    public async Task Handle_FinishedStageOfACompletedProcedure_UnlocksItAndReopensTheProcedure()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.Completed, finishedStages: 3));

        var response = await Handler.Handle(new UnlockProcedureStageCommand(procedure.Id, procedure.StageAt(2).Id, Admin), CancellationToken.None);

        response.Status.Should().Be("InProgress");
        response.Stages[1].IsLocked.Should().BeFalse();
        response.Stages[1].CanEdit.Should().BeTrue();
        response.Stages[0].CanEdit.Should().BeFalse();
        SavedAudit.Select(e => e.Action).Should().Equal(PatientProcedureAuditAction.StageUnlocked, PatientProcedureAuditAction.StatusChanged);
        VerifySavedOnce();
    }
}
