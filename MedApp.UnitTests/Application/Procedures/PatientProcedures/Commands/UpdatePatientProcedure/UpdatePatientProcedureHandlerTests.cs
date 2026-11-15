using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;
using MedApp.Application.Procedures.PatientProcedures.Commands.UpdatePatientProcedure;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Commands.UpdatePatientProcedure;

[TestFixture]
public sealed class UpdatePatientProcedureHandlerTests : ProcedureHandlerTestBase
{
    private UpdatePatientProcedureHandler Handler => Fixture.Create<UpdatePatientProcedureHandler>();

    [Test]
    public async Task Handle_ProcedureDoesNotExist_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new UpdatePatientProcedureCommand(Guid.NewGuid(), "x", null, null, null, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [TestCase(Status.Cancelled)]
    [TestCase(Status.Archived)]
    public async Task Handle_ProcedureClosedOff_ThrowsConflict(Status status)
    {
        var procedure = GivenProcedure(CreateProcedure(status));

        var act = () => Handler.Handle(new UpdatePatientProcedureCommand(procedure.Id, "x", null, null, null, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Handle_FieldsChanged_UpdatesThemAndAuditsWhatChanged()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var command = new UpdatePatientProcedureCommand(procedure.Id, " New title ", null, Today.AddDays(20), "Urgent", Staff);

        var response = await Handler.Handle(command, CancellationToken.None);

        procedure.Title.Should().Be("New title");
        procedure.Priority.Should().Be(PatientProcedurePriority.Urgent);
        response.DueDate.Should().Be(Today.AddDays(20));
        var entry = SavedAudit.Should().ContainSingle().Subject;
        entry.Action.Should().Be(PatientProcedureAuditAction.DetailsUpdated);
        entry.Details.Should().Contain("\"title\"").And.Contain("\"priority\"").And.Contain("\"dueDate\"")
            .And.NotContain("\"notes\"");
        VerifySavedOnce();
    }

    [Test]
    public async Task Handle_NotesChangedOnly_UpdatesThemAndAuditsJustTheNotes()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var command = new UpdatePatientProcedureCommand(procedure.Id, null, "Bring previous scans.", null, null, Staff);

        var response = await Handler.Handle(command, CancellationToken.None);

        procedure.Notes.Should().Be("Bring previous scans.");
        procedure.Title.Should().Be(ProcedureTitle);
        procedure.LastUpdated.Should().Be(Now);
        response.Notes.Should().Be("Bring previous scans.");
        var entry = SavedAudit.Should().ContainSingle().Subject;
        entry.Action.Should().Be(PatientProcedureAuditAction.DetailsUpdated);
        entry.Details.Should().Contain("\"notes\":{\"from\":\"\",\"to\":\"Bring previous scans.\"}")
            .And.NotContain("\"title\"");
        VerifySavedOnce();
    }

    [Test]
    public async Task Handle_SameTitleWithSpaces_ChangesNothingAndDoesNotSave()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.Assigned));
        var command = new UpdatePatientProcedureCommand(procedure.Id, $"  {ProcedureTitle}  ", null, null, null, Staff);

        await Handler.Handle(command, CancellationToken.None);

        procedure.Title.Should().Be(ProcedureTitle);
        procedure.LastUpdated.Should().Be(Now.AddDays(-7));
        SavedAudit.Should().BeEmpty();
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_NothingChanged_DoesNotSave()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.Assigned));
        var command = new UpdatePatientProcedureCommand(procedure.Id, ProcedureTitle, string.Empty, null, "medium", Staff);

        await Handler.Handle(command, CancellationToken.None);

        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_PassesCancellationToken_ToRepository()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.Assigned));
        using var cts = new CancellationTokenSource();

        await Handler.Handle(new UpdatePatientProcedureCommand(procedure.Id, "Renamed", null, null, null, Staff), cts.Token);

        Repository.Verify(r => r.GetForUpdateAsync(procedure.Id, cts.Token), Times.Once);
        VerifySavedOnce(cts.Token);
    }
}
