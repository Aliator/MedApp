using FluentAssertions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.NoteDeletionRequests.Common;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Pathways.NoteDeletionRequests.Common;

[TestFixture]
public sealed class NoteDeletionRequestMapperTests
{
    [Test]
    public void ToResponse_Pending_HasTheNotePatientPathwayAndRequestAndCanBeDecided()
    {
        var pathway = CreatePathway(Status.InProgress);
        var note = pathway.AddNote(OtherUserId, version: 2);
        var request = pathway.RequestDeletion(note);

        var response = NoteDeletionRequestMapper.ToResponse(request, pathway, pathway.Patient, Usernames);

        response.Should().BeEquivalentTo(new
        {
            request.Id,
            Status = "Pending",
            Reason = "WrongPatient",
            Detail = DeletionDetail,
            RequestedByUsername = Staff.Username,
            request.RequestedAtUtc,
            DecidedByUsername = (string?)null,
            DecidedAtUtc = (DateTime?)null,
            PathwayId = pathway.Id,
            PathwayTitle = PathwayTitle,
            Patient = PathwayMapper.ToPatient(pathway.Patient),
            Note = new
            {
                note.Id,
                Text = NoteText,
                AuthorUsername = OtherUsername,
                note.CreatedAtUtc,
                note.LastEditedAtUtc,
                Version = 2,
                DeletedAtUtc = (DateTime?)null,
                DeletedByUsername = (string?)null,
                DeletionReason = (string?)null,
                DeletionDetail = (string?)null
            },
            CanDecide = true,
            WhyCantDecide = (string?)null
        });
    }

    [Test]
    public void ToResponse_Approved_ShowsWhoDeletedTheNoteAndWhy()
    {
        var pathway = CreatePathway(Status.InProgress);
        var note = pathway.AddNote();
        var request = pathway.RequestDeletion(note);
        new PathwayChange(pathway, Admin, Now).DeleteNote(note, NoteDeletionReason.Duplicate, DeletionDetail, request.Id);
        request.Close(NoteDeletionRequestStatus.Approved, Admin.UserId, Admin.Username, Now);

        var response = NoteDeletionRequestMapper.ToResponse(request, pathway, pathway.Patient, Usernames);

        response.Should().BeEquivalentTo(new
        {
            Status = "Approved",
            DecidedByUsername = Admin.Username,
            DecidedAtUtc = (DateTime?)Now,
            CanDecide = false,
            Note = new { DeletedAtUtc = (DateTime?)Now, DeletedByUsername = Admin.Username, DeletionReason = "Duplicate", DeletionDetail }
        });
    }

    [Test]
    public void ToResponse_DeletersAccountGone_IsDeletedUser()
    {
        var pathway = CreatePathway(Status.InProgress);
        var note = pathway.AddNote();
        var request = pathway.RequestDeletion(note);
        new PathwayChange(pathway, new PathwayActor(Guid.NewGuid(), "gone.admin"), Now).DeleteNote(note, NoteDeletionReason.WrongPatient, null);

        NoteDeletionRequestMapper.ToResponse(request, pathway, pathway.Patient, Usernames).Note!.DeletedByUsername
            .Should().Be(PathwayMapper.DeletedUser);
    }

    [Test]
    public void ToResponse_PatientRestricted_CantBeDecidedAndSaysWhy()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.RestrictedAtUtc = Now;
        var pathway = CreatePathway(Status.InProgress, patient: patient);
        var request = pathway.RequestDeletion(pathway.AddNote());

        var response = NoteDeletionRequestMapper.ToResponse(request, pathway, patient, Usernames);

        response.CanDecide.Should().BeFalse();
        response.WhyCantDecide.Should().Be(PatientRules.RestrictedMessage);
        response.Patient!.IsRestricted.Should().BeTrue();
    }

    [Test]
    public void ToResponse_Decided_HasNoReasonItCantBeDecided()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.RestrictedAtUtc = Now;
        var pathway = CreatePathway(Status.InProgress, patient: patient);
        var request = pathway.RequestDeletion(pathway.AddNote());
        request.Close(NoteDeletionRequestStatus.Declined, Admin.UserId, Admin.Username, Now);

        NoteDeletionRequestMapper.ToResponse(request, pathway, patient, Usernames).WhyCantDecide.Should().BeNull();
    }

    [Test]
    public void ToResponse_PathwayDeleted_HasNoNoteOrTitleAndCantBeDecided()
    {
        var pathway = CreatePathway(Status.Cancelled);
        var request = pathway.RequestDeletion(pathway.AddNote());

        var response = NoteDeletionRequestMapper.ToResponse(request, null, pathway.Patient, Usernames);

        response.Note.Should().BeNull();
        response.PathwayTitle.Should().BeNull();
        response.PathwayId.Should().Be(pathway.Id);
        response.CanDecide.Should().BeFalse();
    }

    [Test]
    public void UserIds_AreTheNotesAuthorAndDeleter()
    {
        var pathway = CreatePathway(Status.InProgress);
        var note = pathway.AddNote(OtherUserId);
        var request = pathway.RequestDeletion(note);
        new PathwayChange(pathway, Admin, Now).DeleteNote(note, NoteDeletionReason.WrongPatient, null);

        NoteDeletionRequestMapper.UserIds(request, pathway).Should().BeEquivalentTo([OtherUserId, Admin.UserId]);
        NoteDeletionRequestMapper.UserIds(request, null).Should().BeEmpty();
    }

    [TestCase(NoteDeletionReason.WrongPatient, null, "Wrong patient")]
    [TestCase(NoteDeletionReason.WrittenInError, " ", "Written in error")]
    [TestCase(NoteDeletionReason.Duplicate, "Of the 3 June note.", "Duplicate: Of the 3 June note.")]
    [TestCase(NoteDeletionReason.Other, "Test entry.", "Other: Test entry.")]
    public void Describe_EachReason_InWords(NoteDeletionReason reason, string? detail, string expected) =>
        NoteDeletionReasons.Describe(reason, detail).Should().Be(expected);

    [Test]
    public void NoteDeletionReason_KeepsItsStoredNumbers() =>
        Enum.GetValues<NoteDeletionReason>().Select(r => (int)r).Should().Equal(0, 1, 2, 3);
}
