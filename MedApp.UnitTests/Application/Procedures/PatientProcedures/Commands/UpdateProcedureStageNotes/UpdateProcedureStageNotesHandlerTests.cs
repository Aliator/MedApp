using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;
using MedApp.Application.Procedures.PatientProcedures.Commands.UpdateProcedureStageNotes;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Commands.UpdateProcedureStageNotes;

[TestFixture]
public sealed class UpdateProcedureStageNotesHandlerTests : ProcedureHandlerTestBase
{
    private UpdateProcedureStageNotesHandler Handler => Fixture.Create<UpdateProcedureStageNotesHandler>();

    [Test]
    public async Task Handle_FinishedLockedStage_ThrowsConflict()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress, finishedStages: 1));

        var act = () => Handler.Handle(
            new UpdateProcedureStageNotesCommand(procedure.Id, procedure.StageAt(1).Id, "Changed", Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*locked*");
        procedure.StageAt(1).Notes.Should().Be("Done.");
    }

    [Test]
    public async Task Handle_CurrentStage_SavesTheNotes()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress, finishedStages: 1));
        using var cts = new CancellationTokenSource();

        var response = await Handler.Handle(
            new UpdateProcedureStageNotesCommand(procedure.Id, procedure.StageAt(2).Id, "Went well.", Staff), cts.Token);

        response.Stages[1].Notes.Should().Be("Went well.");
        SavedAudit.Should().Contain(e => e.Action == PatientProcedureAuditAction.StageNotesUpdated && e.StageId == procedure.StageAt(2).Id);
        VerifySavedOnce(cts.Token);
    }

    [Test]
    public async Task Handle_FinishedStageUnlockedByAnAdmin_SavesTheNotes()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress, finishedStages: 2));
        procedure.StageAt(1).IsLocked = false;

        await Handler.Handle(new UpdateProcedureStageNotesCommand(procedure.Id, procedure.StageAt(1).Id, "Corrected.", Staff), CancellationToken.None);

        procedure.StageAt(1).Notes.Should().Be("Corrected.");
        procedure.StageAt(1).IsCompleted.Should().BeTrue();
    }

    [Test]
    public async Task Handle_NotesUnchanged_DoesNotSave()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.Assigned));

        await Handler.Handle(new UpdateProcedureStageNotesCommand(procedure.Id, procedure.StageAt(1).Id, string.Empty, Staff), CancellationToken.None);

        VerifyNotSaved();
    }
}
