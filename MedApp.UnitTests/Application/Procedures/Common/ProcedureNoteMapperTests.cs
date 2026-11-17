using FluentAssertions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.UnitTests.Application.Procedures.Common;

[TestFixture]
public sealed class ProcedureNoteMapperTests
{
    [Test]
    public void ToResponse_Note_HasItsAuthorTimesAndVersion()
    {
        var note = CreateProcedure().AddNote(Staff.UserId, version: 2);

        var response = ProcedureNoteMapper.ToResponse(note, Usernames, Staff.UserId, canChange: true);

        response.Should().BeEquivalentTo(new
        {
            note.Id,
            AuthorUserId = (Guid?)Staff.UserId,
            AuthorUsername = Staff.Username,
            Text = NoteText,
            note.CreatedAtUtc,
            note.LastEditedAtUtc,
            Version = 2,
            CanEdit = true
        });
    }

    [Test]
    public void ToResponse_AuthorsAccountGone_IsDeletedUser()
    {
        var note = CreateProcedure().AddNote(Guid.NewGuid());

        ProcedureNoteMapper.ToResponse(note, Usernames, Staff.UserId, canChange: true).AuthorUsername
            .Should().Be(ProcedureMapper.DeletedUser);
    }

    [Test]
    public void ToResponse_NoAuthorRecorded_IsUnknownAndCantBeEdited()
    {
        var note = new PatientProcedureNote { Id = Guid.NewGuid(), AuthorUserId = null, Text = NoteText, Version = 1 };

        var response = ProcedureNoteMapper.ToResponse(note, Usernames, null, canChange: true);

        response.AuthorUsername.Should().Be(ProcedureNoteMapper.UnknownAuthor);
        response.CanEdit.Should().BeFalse("a null viewer must not match a null author");
    }

    [Test]
    public void ToResponse_SomeoneElsesNote_CantBeEdited()
    {
        var note = CreateProcedure().AddNote(OtherUserId);

        ProcedureNoteMapper.ToResponse(note, Usernames, Staff.UserId, canChange: true).CanEdit.Should().BeFalse();
        ProcedureNoteMapper.ToResponse(note, Usernames, Admin.UserId, canChange: true).CanEdit.Should().BeFalse();
    }

    [Test]
    public void ToResponse_OwnNoteWhileNotesCantChange_CantBeEdited()
    {
        var note = CreateProcedure().AddNote(Staff.UserId);

        ProcedureNoteMapper.ToResponse(note, Usernames, Staff.UserId, canChange: false).CanEdit.Should().BeFalse();
    }

    [Test]
    public void ToResponses_AreNewestFirst()
    {
        var procedure = CreateProcedure();
        var oldest = procedure.AddNote(createdAt: Now.AddDays(-3));
        var newest = procedure.AddNote(createdAt: Now.AddHours(-1));
        var middle = procedure.AddNote(createdAt: Now.AddDays(-1));

        ProcedureNoteMapper.ToResponses(procedure.Notes, Usernames, Staff.UserId, canChange: true).Select(n => n.Id)
            .Should().Equal(newest.Id, middle.Id, oldest.Id);
    }

    [Test]
    public void FirstNoteText_IsTheOldestNotesText()
    {
        var procedure = CreateProcedure();
        procedure.LegacyNotes = "Synthetic old field.";
        procedure.AddNote(text: "Synthetic newer note.", createdAt: Now.AddHours(-1));
        procedure.AddNote(text: "Synthetic first note.", createdAt: Now.AddDays(-3));

        ProcedureNoteMapper.FirstNoteText(procedure).Should().Be("Synthetic first note.");
    }

    [Test]
    public void FirstNoteText_NoNotes_IsTheOldField()
    {
        var procedure = CreateProcedure();
        procedure.LegacyNotes = "Synthetic text an older copy of the API wrote.";

        ProcedureNoteMapper.FirstNoteText(procedure).Should().Be(procedure.LegacyNotes);
    }

    [Test]
    public void ProcedureMapper_ToResponse_KeepsTheOldNotesStringForOldPages()
    {
        var procedure = CreateProcedure();
        procedure.AddNote(text: "Synthetic first note.", createdAt: Now.AddDays(-3));
        procedure.AddNote(text: "Synthetic newer note.", createdAt: Now.AddHours(-1));

        var response = ProcedureMapper.ToResponse(procedure, Usernames, Now, viewerId: Staff.UserId);

        response.Notes.Should().Be("Synthetic first note.");
        response.NoteList.Should().HaveCount(2);
    }

    [Test]
    public void ProcedureMapper_ToResponse_CarriesTheNotesForTheViewer()
    {
        var procedure = CreateProcedure(Status.Completed, finishedStages: 3);
        var own = procedure.AddNote(Staff.UserId);
        procedure.AddNote(OtherUserId);

        var response = ProcedureMapper.ToResponse(procedure, Usernames, Now, viewerId: Staff.UserId);

        response.CanAddNotes.Should().BeTrue("notes can be added after completion");
        response.NoteList.Should().HaveCount(2);
        response.NoteList.Single(n => n.CanEdit).Id.Should().Be(own.Id);
        ProcedureMapper.UserIds(procedure).Should().Contain(OtherUserId);
    }

    [TestCase(Status.Cancelled)]
    [TestCase(Status.Archived)]
    public void ProcedureMapper_ToResponse_ClosedProcedure_CantHaveNotesAddedOrEdited(Status status)
    {
        var procedure = CreateProcedure(status);
        procedure.AddNote(Staff.UserId);

        var response = ProcedureMapper.ToResponse(procedure, Usernames, Now, viewerId: Staff.UserId);

        response.CanAddNotes.Should().BeFalse();
        response.NoteList.Should().ContainSingle().Which.CanEdit.Should().BeFalse();
    }

    [Test]
    public void ProcedureMapper_ToResponse_RestrictedPatient_CantHaveNotesAddedOrEdited()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.RestrictedAtUtc = Now;
        var procedure = CreateProcedure(Status.InProgress, patient: patient);
        procedure.AddNote(Staff.UserId);

        var response = ProcedureMapper.ToResponse(procedure, Usernames, Now, viewerId: Staff.UserId);

        response.ReadOnlyReason.Should().Be(PatientRules.WhyCantChange(patient));
        response.CanAddNotes.Should().BeFalse();
        response.NoteList.Single().CanEdit.Should().BeFalse();
    }
}
