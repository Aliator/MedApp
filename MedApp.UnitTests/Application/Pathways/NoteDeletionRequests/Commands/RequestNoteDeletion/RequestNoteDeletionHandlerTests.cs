using MedApp.Application.Common.Identity;
using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.NoteDeletionRequests.Commands.RequestNoteDeletion;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Pathways.NoteDeletionRequests.Commands.RequestNoteDeletion;

[TestFixture]
public sealed class RequestNoteDeletionHandlerTests : PathwayHandlerTestBase
{
    private Mock<INoteDeletionRequestRepository> _requests = null!;
    private List<PathwayNoteDeletionRequest> _added = null!;
    private List<Notification> _sent = null!;
    private Mock<IPermissionHolders> _holders = null!;

    [SetUp]
    public void SetUp()
    {
        _requests = new Mock<INoteDeletionRequestRepository>();
        _added = [];
        _requests.Setup(r => r.AddAsync(It.IsAny<PathwayNoteDeletionRequest>(), It.IsAny<CancellationToken>()))
            .Callback((PathwayNoteDeletionRequest r, CancellationToken _) => _added.Add(r))
            .Returns(Task.CompletedTask);
        _sent = [];
        var sender = new Mock<INotificationSender>();
        sender.Setup(s => s.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback((Notification n, CancellationToken _) => _sent.Add(n))
            .Returns(Task.CompletedTask);
        // Two holders of CorrectRecords, and the requester holds it too: everyone but them is told.
        _holders = new Mock<IPermissionHolders>();
        _holders.Setup(h => h.GetUserIdsAsync(Permission.CorrectRecords, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Admin.UserId, OtherUserId, Staff.UserId]);
        Fixture.Inject(_holders.Object);
        Fixture.Inject(_requests.Object);
        Fixture.Inject(sender.Object);
    }

    private RequestNoteDeletionHandler Handler => Fixture.Create<RequestNoteDeletionHandler>();

    private static RequestNoteDeletionCommand Command(PatientPathway pathway, Guid noteId, string reason = "WrongPatient", string? detail = DeletionDetail) =>
        new(pathway.Id, noteId, reason, detail, Staff);

    [Test]
    public async Task Handle_Note_AddsAPendingRequestThatTheNotePointsAt()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var note = pathway.AddNote(OtherUserId);

        await Handler.Handle(Command(pathway, note.Id, "writtenInError", $" {DeletionDetail} "), CancellationToken.None);

        var request = _added.Should().ContainSingle().Subject;
        request.Should().BeEquivalentTo(new
        {
            NoteId = note.Id,
            PatientPathwayId = pathway.Id,
            pathway.PatientId,
            RequestedByUserId = Staff.UserId,
            RequestedByUsername = Staff.Username,
            Reason = NoteDeletionReason.WrittenInError,
            Detail = DeletionDetail,
            RequestedAtUtc = Now,
            Status = NoteDeletionRequestStatus.Pending,
            DecidedByUserId = (Guid?)null
        });
        note.PendingDeletionRequestId.Should().Be(request.Id);
        note.IsDeleted.Should().BeFalse();
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_NoDetail_KeepsNone()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var note = pathway.AddNote();

        await Handler.Handle(Command(pathway, note.Id, "Duplicate", "  "), CancellationToken.None);

        _added.Single().Detail.Should().BeNull();
    }

    [Test]
    public async Task Handle_TellsEveryAdminButTheRequester()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var note = pathway.AddNote();

        await Handler.Handle(Command(pathway, note.Id), CancellationToken.None);

        var requestId = _added.Single().Id;
        _sent.Select(n => n.UserId).Should().BeEquivalentTo([Admin.UserId, OtherUserId]);
        _sent.Should().AllSatisfy(n => n.Should().BeEquivalentTo(new
        {
            Kind = NotificationKind.NoteDeletionRequested,
            Title = "Note deletion requested",
            Message = $"{Staff.Username} asked to delete a note on a pathway.",
            PathwayId = (Guid?)pathway.Id,
            NoteDeletionRequestId = (Guid?)requestId
        }));
    }

    [Test]
    public async Task Handle_ReturnsTheNoteShowingTheRequest()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var note = pathway.AddNote(Staff.UserId);

        var response = await Handler.Handle(Command(pathway, note.Id), CancellationToken.None);

        response.Should().BeEquivalentTo(new
        {
            note.Id,
            AuthorUsername = Staff.Username,
            DeletionRequestId = (Guid?)_added.Single().Id,
            CanRequestDeletion = false,
            CanDelete = false,
            CanEdit = true
        });
    }

    [Test]
    public async Task Handle_AlreadyRequested_ThrowsConflictNamingWhoAndWhen()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var note = pathway.AddNote();
        var pending = pathway.RequestDeletion(note, new PathwayActor(OtherUserId, OtherUsername));
        _requests.Setup(r => r.GetByIdAsync(pending.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pending);

        var act = () => Handler.Handle(Command(pathway, note.Id), CancellationToken.None);

        // A day before Now (midday UTC in June): 13:00 BST.
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"{OtherUsername} already asked for this note to be deleted, Sunday 14 June at 13:00. An admin will decide.");
        _added.Should().BeEmpty();
        _sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_DeletedNote_ThrowsConflict()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var note = pathway.AddNote();
        note.DeletedAtUtc = Now.AddDays(-1);

        var act = () => Handler.Handle(Command(pathway, note.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PathwayNotes.DeletedMessage);
        _added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_PatientRestricted_ThrowsConflict()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.RestrictedAtUtc = Now.AddDays(-1);
        var pathway = GivenPathway(CreatePathway(Status.InProgress, patient: patient));
        var note = pathway.AddNote();

        var act = () => Handler.Handle(Command(pathway, note.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _added.Should().BeEmpty();
        note.PendingDeletionRequestId.Should().BeNull();
    }

    [TestCase(Status.Cancelled)]
    [TestCase(Status.Archived)]
    public async Task Handle_PathwayClosed_StillRequests(Status status)
    {
        var pathway = GivenPathway(CreatePathway(status));
        var note = pathway.AddNote();

        var response = await Handler.Handle(Command(pathway, note.Id), CancellationToken.None);

        _added.Should().ContainSingle();
        response.CanEdit.Should().BeFalse("notes can't be edited on a closed pathway");
    }

    [Test]
    public async Task Handle_NoteNotOnThePathway_ThrowsNotFound()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));

        var act = () => Handler.Handle(Command(pathway, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_PassesCancellationToken()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var note = pathway.AddNote();
        using var cts = new CancellationTokenSource();

        await Handler.Handle(Command(pathway, note.Id), cts.Token);

        Repository.Verify(r => r.GetForUpdateAsync(pathway.Id, cts.Token), Times.Once);
        _requests.Verify(r => r.AddAsync(It.IsAny<PathwayNoteDeletionRequest>(), cts.Token), Times.Once);
        _holders.Verify(h => h.GetUserIdsAsync(Permission.CorrectRecords, cts.Token), Times.Once);
    }
}
