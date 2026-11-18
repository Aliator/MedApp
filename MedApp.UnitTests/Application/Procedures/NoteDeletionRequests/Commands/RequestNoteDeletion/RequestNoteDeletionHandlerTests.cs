using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.NoteDeletionRequests.Commands.RequestNoteDeletion;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Notifications;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.UnitTests.Application.Procedures.NoteDeletionRequests.Commands.RequestNoteDeletion;

[TestFixture]
public sealed class RequestNoteDeletionHandlerTests : ProcedureHandlerTestBase
{
    private Mock<INoteDeletionRequestRepository> _requests = null!;
    private List<ProcedureNoteDeletionRequest> _added = null!;
    private List<Notification> _sent = null!;

    [SetUp]
    public void SetUp()
    {
        _requests = new Mock<INoteDeletionRequestRepository>();
        _added = [];
        _requests.Setup(r => r.AddAsync(It.IsAny<ProcedureNoteDeletionRequest>(), It.IsAny<CancellationToken>()))
            .Callback((ProcedureNoteDeletionRequest r, CancellationToken _) => _added.Add(r))
            .Returns(Task.CompletedTask);
        _sent = [];
        var sender = new Mock<INotificationSender>();
        sender.Setup(s => s.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback((Notification n, CancellationToken _) => _sent.Add(n))
            .Returns(Task.CompletedTask);
        // Two admins, and the requester has the Admin role too: everyone but them is told.
        Identity.Setup(i => i.GetAdminIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Admin.UserId, OtherUserId, Staff.UserId]);
        Fixture.Inject(_requests.Object);
        Fixture.Inject(sender.Object);
    }

    private RequestNoteDeletionHandler Handler => Fixture.Create<RequestNoteDeletionHandler>();

    private static RequestNoteDeletionCommand Command(PatientProcedure procedure, Guid noteId, string reason = "WrongPatient", string? detail = DeletionDetail) =>
        new(procedure.Id, noteId, reason, detail, Staff);

    [Test]
    public async Task Handle_Note_AddsAPendingRequestThatTheNotePointsAt()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote(OtherUserId);

        await Handler.Handle(Command(procedure, note.Id, "writtenInError", $" {DeletionDetail} "), CancellationToken.None);

        var request = _added.Should().ContainSingle().Subject;
        request.Should().BeEquivalentTo(new
        {
            NoteId = note.Id,
            PatientProcedureId = procedure.Id,
            procedure.PatientId,
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
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote();

        await Handler.Handle(Command(procedure, note.Id, "Duplicate", "  "), CancellationToken.None);

        _added.Single().Detail.Should().BeNull();
    }

    [Test]
    public async Task Handle_TellsEveryAdminButTheRequester()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote();

        await Handler.Handle(Command(procedure, note.Id), CancellationToken.None);

        var requestId = _added.Single().Id;
        _sent.Select(n => n.UserId).Should().BeEquivalentTo([Admin.UserId, OtherUserId]);
        _sent.Should().AllSatisfy(n => n.Should().BeEquivalentTo(new
        {
            Kind = NotificationKind.NoteDeletionRequested,
            Title = "Note deletion requested",
            Message = $"{Staff.Username} asked to delete a note on a procedure.",
            ProcedureId = (Guid?)procedure.Id,
            NoteDeletionRequestId = (Guid?)requestId
        }));
    }

    [Test]
    public async Task Handle_ReturnsTheNoteShowingTheRequest()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote(Staff.UserId);

        var response = await Handler.Handle(Command(procedure, note.Id), CancellationToken.None);

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
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote();
        var pending = procedure.RequestDeletion(note, new ProcedureActor(OtherUserId, OtherUsername));
        _requests.Setup(r => r.GetByIdAsync(pending.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pending);

        var act = () => Handler.Handle(Command(procedure, note.Id), CancellationToken.None);

        // A day before Now (midday UTC in June): 13:00 BST.
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"{OtherUsername} already asked for this note to be deleted, Sunday 14 June at 13:00. An admin will decide.");
        _added.Should().BeEmpty();
        _sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_DeletedNote_ThrowsConflict()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote();
        note.DeletedAtUtc = Now.AddDays(-1);

        var act = () => Handler.Handle(Command(procedure, note.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(ProcedureNotes.DeletedMessage);
        _added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_PatientRestricted_ThrowsConflict()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.RestrictedAtUtc = Now.AddDays(-1);
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress, patient: patient));
        var note = procedure.AddNote();

        var act = () => Handler.Handle(Command(procedure, note.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _added.Should().BeEmpty();
        note.PendingDeletionRequestId.Should().BeNull();
    }

    [TestCase(Status.Cancelled)]
    [TestCase(Status.Archived)]
    public async Task Handle_ProcedureClosed_StillRequests(Status status)
    {
        var procedure = GivenProcedure(CreateProcedure(status));
        var note = procedure.AddNote();

        var response = await Handler.Handle(Command(procedure, note.Id), CancellationToken.None);

        _added.Should().ContainSingle();
        response.CanEdit.Should().BeFalse("notes can't be edited on a closed procedure");
    }

    [Test]
    public async Task Handle_NoteNotOnTheProcedure_ThrowsNotFound()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));

        var act = () => Handler.Handle(Command(procedure, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_PassesCancellationToken()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote();
        using var cts = new CancellationTokenSource();

        await Handler.Handle(Command(procedure, note.Id), cts.Token);

        Repository.Verify(r => r.GetForUpdateAsync(procedure.Id, cts.Token), Times.Once);
        _requests.Verify(r => r.AddAsync(It.IsAny<ProcedureNoteDeletionRequest>(), cts.Token), Times.Once);
        Identity.Verify(i => i.GetAdminIdsAsync(cts.Token), Times.Once);
    }
}
