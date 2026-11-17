using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.PatientProcedures.Commands.EditProcedureNote;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Commands.EditProcedureNote;

[TestFixture]
public sealed class EditProcedureNoteHandlerTests : ProcedureHandlerTestBase
{
    private const string Edited = "Synthetic note: prefers an afternoon appointment.";

    private EditProcedureNoteHandler Handler => Fixture.Create<EditProcedureNoteHandler>();

    [Test]
    public async Task Handle_OwnNote_SavesTheNextVersionAndAuditsItsIdOnly()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.Completed, finishedStages: 3));
        var note = procedure.AddNote(Staff.UserId);

        var response = await Handler.Handle(new EditProcedureNoteCommand(procedure.Id, note.Id, Edited, 1, Staff), CancellationToken.None);

        note.Should().BeEquivalentTo(new { Text = Edited, Version = 2, LastEditedAtUtc = (DateTime?)Now });
        procedure.LastUpdated.Should().Be(Now);
        var change = SavedChanges.Should().ContainSingle().Subject;
        change.NewNotes.Should().BeEmpty();
        change.NoteVersions.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            NoteId = note.Id, Version = 2, Text = Edited, UserId = (Guid?)Staff.UserId, Staff.Username, AtUtc = Now
        });
        var entry = SavedAudit.Should().ContainSingle().Subject;
        entry.Action.Should().Be(PatientProcedureAuditAction.NoteEdited);
        entry.Details.Should().Be($$"""{"noteId":"{{note.Id}}","version":2}""").And.NotContain(Edited).And.NotContain(NoteText);
        response.Should().BeEquivalentTo(new { note.Id, Text = Edited, Version = 2, LastEditedAtUtc = (DateTime?)Now, CanEdit = true });
        VerifySavedOnce();
    }

    [Test]
    public async Task Handle_TextUnchanged_SavesNothing()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote(Staff.UserId);

        var response = await Handler.Handle(new EditProcedureNoteCommand(procedure.Id, note.Id, NoteText, 1, Staff), CancellationToken.None);

        response.Version.Should().Be(1);
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_SomeoneElsesNote_ThrowsForbidden()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote(OtherUserId);

        var act = () => Handler.Handle(new EditProcedureNoteCommand(procedure.Id, note.Id, Edited, 1, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage(ProcedureNotes.OnlyTheAuthorMessage);
        note.Text.Should().Be(NoteText);
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_AdminEditingSomeoneElsesNote_ThrowsForbidden()
    {
        // Clinical record integrity: what someone wrote stays theirs, admins included.
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote(Staff.UserId);

        var act = () => Handler.Handle(new EditProcedureNoteCommand(procedure.Id, note.Id, Edited, 1, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_NoteWithNoKnownAuthor_ThrowsForbidden()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = new PatientProcedureNote { Id = Guid.NewGuid(), PatientProcedureId = procedure.Id, AuthorUserId = null, Text = NoteText, Version = 1 };
        procedure.Notes.Add(note);

        var act = () => Handler.Handle(new EditProcedureNoteCommand(procedure.Id, note.Id, Edited, 1, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Test]
    public async Task Handle_StaleVersion_ThrowsConflictWithoutSaving()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote(Staff.UserId, version: 3);

        var act = () => Handler.Handle(new EditProcedureNoteCommand(procedure.Id, note.Id, Edited, 2, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(ProcedureNotes.ChangedMeanwhileMessage);
        note.Version.Should().Be(3);
        VerifyNotSaved();
    }

    [TestCase(Status.Cancelled)]
    [TestCase(Status.Archived)]
    public async Task Handle_ProcedureClosed_ThrowsConflict(Status status)
    {
        var procedure = GivenProcedure(CreateProcedure(status));
        var note = procedure.AddNote(Staff.UserId);

        var act = () => Handler.Handle(new EditProcedureNoteCommand(procedure.Id, note.Id, Edited, 1, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("Notes can't be added or edited while the procedure is *");
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_PatientAnonymised_ThrowsConflict()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.AnonymisedAtUtc = Now.AddDays(-1);
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress, patient: patient));
        var note = procedure.AddNote(Staff.UserId);

        var act = () => Handler.Handle(new EditProcedureNoteCommand(procedure.Id, note.Id, Edited, 1, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_NoteNotOnTheProcedure_ThrowsNotFound()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));

        var act = () => Handler.Handle(new EditProcedureNoteCommand(procedure.Id, Guid.NewGuid(), Edited, 1, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_PassesCancellationToken_ToRepository()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote(Staff.UserId);
        using var cts = new CancellationTokenSource();

        await Handler.Handle(new EditProcedureNoteCommand(procedure.Id, note.Id, Edited, 1, Staff), cts.Token);

        Repository.Verify(r => r.GetForUpdateAsync(procedure.Id, cts.Token), Times.Once);
        VerifySavedOnce(cts.Token);
    }
}
