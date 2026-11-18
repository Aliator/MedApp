using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.PatientPathways.Commands.DeletePathwayNote;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.DeletePathwayNote;

[TestFixture]
public sealed class DeletePathwayNoteHandlerTests : PathwayHandlerTestBase
{
    private Mock<INoteDeletionRequestRepository> _requests = null!;
    private Mock<IPathwayMonitor> _monitor = null!;
    private Mock<INotificationSender> _sender = null!;
    private List<Notification> _sent = null!;

    [SetUp]
    public void SetUp()
    {
        _requests = new Mock<INoteDeletionRequestRepository>();
        _monitor = new Mock<IPathwayMonitor>();
        _sent = [];
        _sender = new Mock<INotificationSender>();
        _sender.Setup(s => s.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback((Notification n, CancellationToken _) => _sent.Add(n))
            .Returns(Task.CompletedTask);
        Fixture.Inject(_requests.Object);
        Fixture.Inject(_monitor.Object);
        Fixture.Inject(_sender.Object);
    }

    private DeletePathwayNoteHandler Handler => Fixture.Create<DeletePathwayNoteHandler>();

    private static DeletePathwayNoteCommand Command(
        PatientPathway pathway, PatientPathwayNote note, string reason = "WrongPatient", string? detail = DeletionDetail, int? version = null) =>
        new(pathway.Id, note.Id, reason, detail, version ?? note.Version, Admin);

    [Test]
    public async Task Handle_Note_MarksItDeletedAndAuditsItsIdVersionAndReasonOnly()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var note = pathway.AddNote(OtherUserId, version: 2);

        await Handler.Handle(Command(pathway, note, detail: $"  {DeletionDetail}  "), CancellationToken.None);

        note.Should().BeEquivalentTo(new
        {
            DeletedAtUtc = (DateTime?)Now,
            DeletedByUserId = (Guid?)Admin.UserId,
            DeletionReason = (NoteDeletionReason?)NoteDeletionReason.WrongPatient,
            DeletionDetail,
            Text = NoteText,
            Version = 2
        });
        pathway.Notes.Should().Contain(note, "deleting is soft: the note is kept");
        var change = SavedChanges.Should().ContainSingle().Subject;
        change.DeletedNotes.Should().Equal(note);
        change.NoteVersions.Should().BeEmpty();
        var entry = SavedAudit.Should().ContainSingle().Subject;
        entry.Action.Should().Be(PatientPathwayAuditAction.NoteDeleted);
        entry.Details.Should().Be($$"""{"noteId":"{{note.Id}}","version":2,"deletionReason":"WrongPatient","detail":"{{DeletionDetail}}"}""")
            .And.NotContain(NoteText);
        _monitor.Verify(m => m.NotifyAsync(change, CancellationToken.None), Times.Once);
        VerifySavedOnce();
    }

    [Test]
    public async Task Handle_NoDetail_RecordsTheReasonAlone()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var note = pathway.AddNote();

        await Handler.Handle(Command(pathway, note, reason: "duplicate", detail: "  "), CancellationToken.None);

        note.DeletionReason.Should().Be(NoteDeletionReason.Duplicate);
        note.DeletionDetail.Should().BeNull();
    }

    [Test]
    public async Task Handle_EditedSinceTheAdminOpenedIt_ThrowsConflictWithoutSaving()
    {
        // The admin read version 1; the author has saved version 2 since.
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var note = pathway.AddNote(version: 2);

        var act = () => Handler.Handle(Command(pathway, note, version: 1), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PathwayNotes.EditedSinceOpenedMessage);
        note.IsDeleted.Should().BeFalse();
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_SomeoneElsesNote_TellsItsAuthor()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var note = pathway.AddNote(OtherUserId);

        await Handler.Handle(Command(pathway, note), CancellationToken.None);

        _sent.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            UserId = OtherUserId,
            Kind = NotificationKind.AuthorsNoteDeleted,
            Title = "Your note was deleted",
            Message = $"{Admin.Username} deleted a note you wrote on a pathway.",
            PathwayId = (Guid?)pathway.Id
        });
    }

    [Test]
    public async Task Handle_TheirOwnNote_TellsNobody()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var note = pathway.AddNote(Admin.UserId);

        await Handler.Handle(Command(pathway, note), CancellationToken.None);

        _sent.Should().BeEmpty();
    }

    [TestCase(Status.Cancelled)]
    [TestCase(Status.Archived)]
    [TestCase(Status.Completed)]
    public async Task Handle_PathwayClosed_StillDeletes(Status status)
    {
        // A correction, like an admin deleting a file: allowed whatever the pathway's status.
        var pathway = GivenPathway(CreatePathway(status));
        var note = pathway.AddNote();

        await Handler.Handle(Command(pathway, note), CancellationToken.None);

        note.IsDeleted.Should().BeTrue();
        VerifySavedOnce();
    }

    [Test]
    public async Task Handle_PatientRestricted_ThrowsConflictWithoutSaving()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.RestrictedAtUtc = Now.AddDays(-1);
        var pathway = GivenPathway(CreatePathway(Status.InProgress, patient: patient));
        var note = pathway.AddNote();

        var act = () => Handler.Handle(Command(pathway, note), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        note.IsDeleted.Should().BeFalse();
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_PatientAnonymised_ThrowsConflict()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.AnonymisedAtUtc = Now.AddDays(-1);
        var pathway = GivenPathway(CreatePathway(Status.InProgress, patient: patient));
        var note = pathway.AddNote();

        var act = () => Handler.Handle(Command(pathway, note), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_AlreadyDeleted_ThrowsConflict()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var note = pathway.AddNote();
        note.DeletedAtUtc = Now.AddDays(-1);

        var act = () => Handler.Handle(Command(pathway, note), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PathwayNotes.DeletedMessage);
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_NoteNotOnThePathway_ThrowsNotFound()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));

        var act = () => Handler.Handle(new DeletePathwayNoteCommand(pathway.Id, Guid.NewGuid(), "WrongPatient", null, 1, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_RequestWaiting_ApprovesItAndTellsTheRequesterAndTheAuthor()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var note = pathway.AddNote(OtherUserId);
        var request = pathway.RequestDeletion(note);
        _requests.Setup(r => r.GetForUpdateAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        await Handler.Handle(Command(pathway, note), CancellationToken.None);

        request.Should().BeEquivalentTo(new
        {
            Status = NoteDeletionRequestStatus.Approved,
            DecidedByUserId = (Guid?)Admin.UserId,
            DecidedByUsername = Admin.Username,
            DecidedAtUtc = (DateTime?)Now
        });
        note.PendingDeletionRequestId.Should().BeNull();
        SavedAudit.Single().Details.Should().Contain($"\"requestId\":\"{request.Id}\"");
        _sent.Select(n => (n.UserId, n.Kind)).Should().BeEquivalentTo(
            [(Staff.UserId, NotificationKind.NoteDeletionApproved), (OtherUserId, NotificationKind.AuthorsNoteDeleted)]);
    }

    [Test]
    public async Task Handle_RequestedByItsAuthor_TellsThemOnce()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var note = pathway.AddNote(Staff.UserId);
        var request = pathway.RequestDeletion(note);
        _requests.Setup(r => r.GetForUpdateAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        await Handler.Handle(Command(pathway, note), CancellationToken.None);

        _sent.Should().ContainSingle().Which.Kind.Should().Be(NotificationKind.NoteDeletionApproved);
    }

    [Test]
    public async Task Handle_PassesCancellationToken()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var note = pathway.AddNote(OtherUserId);
        var request = pathway.RequestDeletion(note);
        _requests.Setup(r => r.GetForUpdateAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        using var cts = new CancellationTokenSource();

        await Handler.Handle(Command(pathway, note), cts.Token);

        Repository.Verify(r => r.GetForUpdateAsync(pathway.Id, cts.Token), Times.Once);
        _requests.Verify(r => r.GetForUpdateAsync(request.Id, cts.Token), Times.Once);
        _monitor.Verify(m => m.NotifyAsync(It.IsAny<PathwayChange>(), cts.Token), Times.Once);
        _sender.Verify(s => s.SendAsync(It.IsAny<Notification>(), cts.Token), Times.Exactly(2));
        VerifySavedOnce(cts.Token);
    }
}
