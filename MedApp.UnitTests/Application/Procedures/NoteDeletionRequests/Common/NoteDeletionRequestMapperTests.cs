using FluentAssertions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.NoteDeletionRequests.Common;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.UnitTests.Application.Procedures.NoteDeletionRequests.Common;

[TestFixture]
public sealed class NoteDeletionRequestMapperTests
{
    [Test]
    public void ToResponse_Pending_HasTheNotePatientProcedureAndRequestAndCanBeDecided()
    {
        var procedure = CreateProcedure(Status.InProgress);
        var note = procedure.AddNote(OtherUserId, version: 2);
        var request = procedure.RequestDeletion(note);

        var response = NoteDeletionRequestMapper.ToResponse(request, procedure, procedure.Patient, Usernames);

        response.Should().BeEquivalentTo(new
        {
            request.Id,
            Status = "Pending",
            Reason = DeletionReason,
            RequestedByUsername = Staff.Username,
            request.RequestedAtUtc,
            DecidedByUsername = (string?)null,
            DecidedAtUtc = (DateTime?)null,
            ProcedureId = procedure.Id,
            ProcedureTitle = ProcedureTitle,
            Patient = ProcedureMapper.ToPatient(procedure.Patient),
            Note = new
            {
                note.Id,
                Text = NoteText,
                AuthorUsername = OtherUsername,
                note.CreatedAtUtc,
                note.LastEditedAtUtc,
                DeletedAtUtc = (DateTime?)null,
                DeletedByUsername = (string?)null,
                DeletionReason = (string?)null
            },
            CanDecide = true,
            WhyCantDecide = (string?)null
        });
    }

    [Test]
    public void ToResponse_Approved_ShowsWhoDeletedTheNoteAndWhy()
    {
        var procedure = CreateProcedure(Status.InProgress);
        var note = procedure.AddNote();
        var request = procedure.RequestDeletion(note);
        new ProcedureChange(procedure, Admin, Now).DeleteNote(note, DeletionReason, request.Id);
        request.Close(NoteDeletionRequestStatus.Approved, Admin.UserId, Admin.Username, Now);

        var response = NoteDeletionRequestMapper.ToResponse(request, procedure, procedure.Patient, Usernames);

        response.Should().BeEquivalentTo(new
        {
            Status = "Approved",
            DecidedByUsername = Admin.Username,
            DecidedAtUtc = (DateTime?)Now,
            CanDecide = false,
            Note = new { DeletedAtUtc = (DateTime?)Now, DeletedByUsername = Admin.Username, DeletionReason }
        });
    }

    [Test]
    public void ToResponse_DeletersAccountGone_IsDeletedUser()
    {
        var procedure = CreateProcedure(Status.InProgress);
        var note = procedure.AddNote();
        var request = procedure.RequestDeletion(note);
        new ProcedureChange(procedure, new ProcedureActor(Guid.NewGuid(), "gone.admin"), Now).DeleteNote(note, DeletionReason);

        NoteDeletionRequestMapper.ToResponse(request, procedure, procedure.Patient, Usernames).Note!.DeletedByUsername
            .Should().Be(ProcedureMapper.DeletedUser);
    }

    [Test]
    public void ToResponse_PatientRestricted_CantBeDecidedAndSaysWhy()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.RestrictedAtUtc = Now;
        var procedure = CreateProcedure(Status.InProgress, patient: patient);
        var request = procedure.RequestDeletion(procedure.AddNote());

        var response = NoteDeletionRequestMapper.ToResponse(request, procedure, patient, Usernames);

        response.CanDecide.Should().BeFalse();
        response.WhyCantDecide.Should().Be(PatientRules.RestrictedMessage);
        response.Patient!.IsRestricted.Should().BeTrue();
    }

    [Test]
    public void ToResponse_Decided_HasNoReasonItCantBeDecided()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.RestrictedAtUtc = Now;
        var procedure = CreateProcedure(Status.InProgress, patient: patient);
        var request = procedure.RequestDeletion(procedure.AddNote());
        request.Close(NoteDeletionRequestStatus.Declined, Admin.UserId, Admin.Username, Now);

        NoteDeletionRequestMapper.ToResponse(request, procedure, patient, Usernames).WhyCantDecide.Should().BeNull();
    }

    [Test]
    public void ToResponse_ProcedureDeleted_HasNoNoteOrTitleAndCantBeDecided()
    {
        var procedure = CreateProcedure(Status.Cancelled);
        var request = procedure.RequestDeletion(procedure.AddNote());

        var response = NoteDeletionRequestMapper.ToResponse(request, null, procedure.Patient, Usernames);

        response.Note.Should().BeNull();
        response.ProcedureTitle.Should().BeNull();
        response.ProcedureId.Should().Be(procedure.Id);
        response.CanDecide.Should().BeFalse();
    }

    [Test]
    public void UserIds_AreTheNotesAuthorAndDeleter()
    {
        var procedure = CreateProcedure(Status.InProgress);
        var note = procedure.AddNote(OtherUserId);
        var request = procedure.RequestDeletion(note);
        new ProcedureChange(procedure, Admin, Now).DeleteNote(note, DeletionReason);

        NoteDeletionRequestMapper.UserIds(request, procedure).Should().BeEquivalentTo([OtherUserId, Admin.UserId]);
        NoteDeletionRequestMapper.UserIds(request, null).Should().BeEmpty();
    }
}
