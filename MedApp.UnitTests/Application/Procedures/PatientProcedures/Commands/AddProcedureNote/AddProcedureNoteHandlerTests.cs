using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.PatientProcedures.Commands.AddProcedureNote;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Commands.AddProcedureNote;

[TestFixture]
public sealed class AddProcedureNoteHandlerTests : ProcedureHandlerTestBase
{
    private AddProcedureNoteHandler Handler => Fixture.Create<AddProcedureNoteHandler>();

    [Test]
    public async Task Handle_ProcedureDoesNotExist_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new AddProcedureNoteCommand(Guid.NewGuid(), NoteText, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [TestCase(Status.Unassigned)]
    [TestCase(Status.InProgress)]
    [TestCase(Status.Blocked)]
    [TestCase(Status.InReview)]
    [TestCase(Status.Completed)]
    public async Task Handle_NotesCanChange_AddsTheNoteAsVersionOneAndAuditsItsIdOnly(Status status)
    {
        var procedure = GivenProcedure(CreateProcedure(status));

        var response = await Handler.Handle(new AddProcedureNoteCommand(procedure.Id, NoteText, Staff), CancellationToken.None);

        var note = procedure.Notes.Should().ContainSingle().Subject;
        note.Should().BeEquivalentTo(new { AuthorUserId = (Guid?)Staff.UserId, Text = NoteText, CreatedAtUtc = Now, Version = 1, LastEditedAtUtc = (DateTime?)null });
        procedure.LastUpdated.Should().Be(Now);

        var change = SavedChanges.Should().ContainSingle().Subject;
        change.NewNotes.Should().Equal(note);
        change.NoteVersions.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            NoteId = note.Id, Version = 1, PatientProcedureId = procedure.Id, procedure.PatientId, Text = NoteText,
            UserId = (Guid?)Staff.UserId, Staff.Username, AtUtc = Now
        });
        var entry = SavedAudit.Should().ContainSingle().Subject;
        entry.Action.Should().Be(PatientProcedureAuditAction.NoteAdded);
        entry.Details.Should().Be($$"""{"noteId":"{{note.Id}}","version":1}""").And.NotContain(NoteText);

        response.Should().BeEquivalentTo(new
        {
            note.Id, AuthorUserId = (Guid?)Staff.UserId, AuthorUsername = Staff.Username, Text = NoteText, CreatedAtUtc = Now,
            Version = 1, CanEdit = true
        });
        VerifySavedOnce();
    }

    [TestCase(Status.Cancelled)]
    [TestCase(Status.Archived)]
    public async Task Handle_ProcedureClosed_ThrowsConflictWithoutSaving(Status status)
    {
        var procedure = GivenProcedure(CreateProcedure(status));

        var act = () => Handler.Handle(new AddProcedureNoteCommand(procedure.Id, NoteText, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("Notes can't be added or edited while the procedure is *");
        procedure.Notes.Should().BeEmpty();
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_PatientRestricted_ThrowsConflictWithoutSaving()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.RestrictedAtUtc = Now.AddDays(-1);
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress, patient: patient));

        var act = () => Handler.Handle(new AddProcedureNoteCommand(procedure.Id, NoteText, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_PassesCancellationToken_ToRepository()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        using var cts = new CancellationTokenSource();

        await Handler.Handle(new AddProcedureNoteCommand(procedure.Id, NoteText, Staff), cts.Token);

        Repository.Verify(r => r.GetForUpdateAsync(procedure.Id, cts.Token), Times.Once);
        VerifySavedOnce(cts.Token);
    }
}
