using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Patients.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.NoteDeletionRequests.Commands.DecideNoteDeletionRequest;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Notifications;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.UnitTests.Application.Procedures.NoteDeletionRequests.Commands.DecideNoteDeletionRequest;

[TestFixture]
public sealed class DecideNoteDeletionRequestHandlerTests : ProcedureHandlerTestBase
{
    private const string AdminsReason = "Synthetic reason: the admin's own words.";

    private Mock<INoteDeletionRequestRepository> _requests = null!;
    private Mock<IProcedureMonitor> _monitor = null!;
    private List<Notification> _sent = null!;
    private List<CancellationToken> _requestSaves = null!;

    [SetUp]
    public void SetUp()
    {
        _requests = new Mock<INoteDeletionRequestRepository>();
        _requestSaves = [];
        _requests.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>()))
            .Callback((CancellationToken t) => _requestSaves.Add(t))
            .Returns(Task.CompletedTask);
        _monitor = new Mock<IProcedureMonitor>();
        _sent = [];
        var sender = new Mock<INotificationSender>();
        sender.Setup(s => s.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback((Notification n, CancellationToken _) => _sent.Add(n))
            .Returns(Task.CompletedTask);
        Fixture.Inject(_requests.Object);
        Fixture.Inject(_monitor.Object);
        Fixture.Inject(sender.Object);
    }

    private DecideNoteDeletionRequestHandler Handler => Fixture.Create<DecideNoteDeletionRequestHandler>();

    private (PatientProcedure Procedure, PatientProcedureNote Note, ProcedureNoteDeletionRequest Request) GivenRequest(
        PatientProcedure? procedure = null)
    {
        procedure = GivenProcedure(procedure ?? CreateProcedure(Status.InProgress));
        var note = procedure.AddNote(OtherUserId);
        var request = procedure.RequestDeletion(note);
        _requests.Setup(r => r.GetForUpdateAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        return (procedure, note, request);
    }

    [Test]
    public async Task Handle_DeleteNote_DeletesItWithTheRequestersReasonAndApprovesTheRequest()
    {
        var (procedure, note, request) = GivenRequest();

        var response = await Handler.Handle(new DecideNoteDeletionRequestCommand(request.Id, true, null, Admin), CancellationToken.None);

        note.Should().BeEquivalentTo(new
        {
            DeletedAtUtc = (DateTime?)Now,
            DeletedByUserId = (Guid?)Admin.UserId,
            DeletionReason,
            PendingDeletionRequestId = (Guid?)null
        });
        request.Should().BeEquivalentTo(new
        {
            Status = NoteDeletionRequestStatus.Approved,
            DecidedByUserId = (Guid?)Admin.UserId,
            DecidedByUsername = Admin.Username,
            DecidedAtUtc = (DateTime?)Now
        });
        var entry = SavedAudit.Should().ContainSingle().Subject;
        entry.Action.Should().Be(PatientProcedureAuditAction.NoteDeleted);
        entry.Details.Should().Be($$"""{"noteId":"{{note.Id}}","version":1,"reason":"{{DeletionReason}}","requestId":"{{request.Id}}"}""");
        VerifySavedOnce();
        _requestSaves.Should().BeEmpty("the request is saved with the procedure's change");
        _monitor.Verify(m => m.NotifyAsync(SavedChanges.Single(), CancellationToken.None), Times.Once);
        response.Should().BeEquivalentTo(new
        {
            request.Id,
            Status = "Approved",
            DecidedByUsername = Admin.Username,
            ProcedureId = procedure.Id,
            ProcedureTitle = procedure.Title,
            CanDecide = false,
            Note = new { note.Id, DeletedByUsername = Admin.Username, DeletionReason }
        });
    }

    [Test]
    public async Task Handle_DeleteNoteWithTheAdminsReason_RecordsThatReason()
    {
        var (_, note, request) = GivenRequest();

        await Handler.Handle(new DecideNoteDeletionRequestCommand(request.Id, true, $" {AdminsReason} ", Admin), CancellationToken.None);

        note.DeletionReason.Should().Be(AdminsReason);
        request.Reason.Should().Be(DeletionReason, "the requester's reason is kept as they wrote it");
    }

    [Test]
    public async Task Handle_DeleteNote_TellsTheRequester()
    {
        var (procedure, _, request) = GivenRequest();

        await Handler.Handle(new DecideNoteDeletionRequestCommand(request.Id, true, null, Admin), CancellationToken.None);

        _sent.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            UserId = Staff.UserId,
            Kind = NotificationKind.NoteDeletionApproved,
            Title = "Note deleted",
            Message = $"{Admin.Username} deleted the note you asked to delete.",
            ProcedureId = (Guid?)procedure.Id
        });
    }

    [Test]
    public async Task Handle_KeepNote_DeclinesTheRequestAndLeavesTheNote()
    {
        var (_, note, request) = GivenRequest();

        var response = await Handler.Handle(new DecideNoteDeletionRequestCommand(request.Id, false, AdminsReason, Admin), CancellationToken.None);

        note.IsDeleted.Should().BeFalse();
        note.PendingDeletionRequestId.Should().BeNull();
        request.Status.Should().Be(NoteDeletionRequestStatus.Declined);
        request.DecidedByUsername.Should().Be(Admin.Username);
        VerifyNotSaved();
        _requestSaves.Should().ContainSingle();
        _monitor.Verify(m => m.NotifyAsync(It.IsAny<ProcedureChange>(), It.IsAny<CancellationToken>()), Times.Never);
        _sent.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            UserId = Staff.UserId,
            Kind = NotificationKind.NoteDeletionDeclined,
            Message = $"{Admin.Username} kept the note you asked to delete."
        });
        response.Should().BeEquivalentTo(new { Status = "Declined", CanDecide = false, Note = new { DeletedAtUtc = (DateTime?)null } });
    }

    [TestCase(NoteDeletionRequestStatus.Approved, "* already deleted this note, Sunday 14 June at 13:00.")]
    [TestCase(NoteDeletionRequestStatus.Declined, "* already decided to keep this note, Sunday 14 June at 13:00.")]
    [TestCase(NoteDeletionRequestStatus.Closed, "This request was closed without a decision*")]
    public async Task Handle_AlreadyDecided_ThrowsConflictSayingHow(NoteDeletionRequestStatus status, string message)
    {
        var (_, note, request) = GivenRequest();
        request.Close(status, OtherUserId, OtherUsername, Now.AddDays(-1));

        var act = () => Handler.Handle(new DecideNoteDeletionRequestCommand(request.Id, true, null, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(message);
        note.IsDeleted.Should().BeFalse();
        VerifyNotSaved();
        _sent.Should().BeEmpty();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Handle_PatientRestricted_ThrowsConflictWithoutSaving(bool deleteNote)
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.RestrictedAtUtc = Now.AddDays(-1);
        var (_, note, request) = GivenRequest(CreateProcedure(Status.InProgress, patient: patient));

        var act = () => Handler.Handle(new DecideNoteDeletionRequestCommand(request.Id, deleteNote, null, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
        request.IsPending.Should().BeTrue();
        note.PendingDeletionRequestId.Should().Be(request.Id);
        VerifyNotSaved();
        _requestSaves.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_RequestNotFound_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new DecideNoteDeletionRequestCommand(Guid.NewGuid(), true, null, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_AdminDecidingTheirOwnRequest_TellsNobody()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote();
        var request = procedure.RequestDeletion(note, Admin);
        _requests.Setup(r => r.GetForUpdateAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        await Handler.Handle(new DecideNoteDeletionRequestCommand(request.Id, false, null, Admin), CancellationToken.None);

        _sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_PassesCancellationToken()
    {
        var (procedure, _, request) = GivenRequest();
        using var cts = new CancellationTokenSource();

        await Handler.Handle(new DecideNoteDeletionRequestCommand(request.Id, false, null, Admin), cts.Token);

        _requests.Verify(r => r.GetForUpdateAsync(request.Id, cts.Token), Times.Once);
        Repository.Verify(r => r.GetForUpdateAsync(procedure.Id, cts.Token), Times.Once);
        _requestSaves.Should().Equal(cts.Token);
    }
}
