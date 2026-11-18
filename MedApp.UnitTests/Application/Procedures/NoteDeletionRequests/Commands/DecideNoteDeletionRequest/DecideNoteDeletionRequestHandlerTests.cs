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
    private const string AdminsDetail = "Synthetic detail: the admin's own words.";

    private Mock<INoteDeletionRequestRepository> _requests = null!;
    private Mock<IProcedureMonitor> _monitor = null!;
    private Mock<INotificationSender> _sender = null!;
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
        _sender = new Mock<INotificationSender>();
        _sender.Setup(s => s.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback((Notification n, CancellationToken _) => _sent.Add(n))
            .Returns(Task.CompletedTask);
        Fixture.Inject(_requests.Object);
        Fixture.Inject(_monitor.Object);
        Fixture.Inject(_sender.Object);
    }

    private DecideNoteDeletionRequestHandler Handler => Fixture.Create<DecideNoteDeletionRequestHandler>();

    private (PatientProcedure Procedure, PatientProcedureNote Note, ProcedureNoteDeletionRequest Request) GivenRequest(
        PatientProcedure? procedure = null, Guid? author = null)
    {
        procedure = GivenProcedure(procedure ?? CreateProcedure(Status.InProgress));
        var note = procedure.AddNote(author ?? OtherUserId);
        var request = procedure.RequestDeletion(note);
        _requests.Setup(r => r.GetForUpdateAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        return (procedure, note, request);
    }

    private static DecideNoteDeletionRequestCommand Delete(ProcedureNoteDeletionRequest request, int? version = 1, string? reason = null, string? detail = null) =>
        new(request.Id, true, version, reason, detail, Admin);

    private static DecideNoteDeletionRequestCommand Keep(ProcedureNoteDeletionRequest request) =>
        new(request.Id, false, null, null, null, Admin);

    [Test]
    public async Task Handle_DeleteNote_DeletesItWithTheRequestersReasonAndApprovesTheRequest()
    {
        var (procedure, note, request) = GivenRequest();

        var response = await Handler.Handle(Delete(request), CancellationToken.None);

        note.Should().BeEquivalentTo(new
        {
            DeletedAtUtc = (DateTime?)Now,
            DeletedByUserId = (Guid?)Admin.UserId,
            DeletionReason = (NoteDeletionReason?)NoteDeletionReason.WrongPatient,
            DeletionDetail,
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
        entry.Details.Should().Be(
            $$"""{"noteId":"{{note.Id}}","version":1,"deletionReason":"WrongPatient","detail":"{{DeletionDetail}}","requestId":"{{request.Id}}"}""");
        VerifySavedOnce();
        _requestSaves.Should().BeEmpty("the request is saved with the procedure's change");
        _monitor.Verify(m => m.NotifyAsync(SavedChanges.Single(), CancellationToken.None), Times.Once);
        response.Should().BeEquivalentTo(new
        {
            request.Id,
            Status = "Approved",
            Reason = "WrongPatient",
            Detail = DeletionDetail,
            DecidedByUsername = Admin.Username,
            ProcedureId = procedure.Id,
            ProcedureTitle = procedure.Title,
            CanDecide = false,
            Note = new { note.Id, Version = 1, DeletedByUsername = Admin.Username, DeletionReason = "WrongPatient", DeletionDetail }
        });
    }

    [Test]
    public async Task Handle_DeleteNoteWithTheAdminsReason_RecordsThatReasonAndKeepsTheRequesters()
    {
        var (_, note, request) = GivenRequest();

        await Handler.Handle(Delete(request, reason: "Duplicate", detail: $" {AdminsDetail} "), CancellationToken.None);

        note.DeletionReason.Should().Be(NoteDeletionReason.Duplicate);
        note.DeletionDetail.Should().Be(AdminsDetail);
        request.Should().BeEquivalentTo(new { Reason = NoteDeletionReason.WrongPatient, Detail = DeletionDetail });
    }

    [Test]
    public async Task Handle_DeleteNoteWithOnlyTheAdminsDetail_KeepsTheRequestersReason()
    {
        var (_, note, request) = GivenRequest();

        await Handler.Handle(Delete(request, detail: AdminsDetail), CancellationToken.None);

        note.DeletionReason.Should().Be(NoteDeletionReason.WrongPatient);
        note.DeletionDetail.Should().Be(AdminsDetail);
    }

    [Test]
    public async Task Handle_DeleteNoteEditedSinceTheAdminOpenedIt_ThrowsConflictWithoutSaving()
    {
        // The author edited it into version 2 after the admin opened the request at version 1.
        var (_, note, request) = GivenRequest();
        note.Version = 2;

        var act = () => Handler.Handle(Delete(request, version: 1), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(ProcedureNotes.EditedSinceOpenedMessage);
        note.IsDeleted.Should().BeFalse();
        request.IsPending.Should().BeTrue();
        VerifyNotSaved();
        _sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_DeleteNote_TellsTheRequesterAndTheAuthor()
    {
        var (procedure, _, request) = GivenRequest();

        await Handler.Handle(Delete(request), CancellationToken.None);

        _sent.Should().HaveCount(2);
        _sent.Should().ContainSingle(n => n.UserId == Staff.UserId).Which.Should().BeEquivalentTo(new
        {
            Kind = NotificationKind.NoteDeletionApproved,
            Title = "Note deleted",
            Message = $"{Admin.Username} deleted the note you asked to delete.",
            ProcedureId = (Guid?)procedure.Id
        });
        _sent.Should().ContainSingle(n => n.UserId == OtherUserId).Which.Kind.Should().Be(NotificationKind.AuthorsNoteDeleted);
    }

    [Test]
    public async Task Handle_KeepNote_DeclinesTheRequestAndLeavesTheNote()
    {
        var (_, note, request) = GivenRequest();

        var response = await Handler.Handle(Keep(request), CancellationToken.None);

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

    [Test]
    public async Task Handle_KeepNoteEditedSinceTheRequest_StillKeepsIt()
    {
        // Keeping needs no version: nothing is lost by keeping a note.
        var (_, note, request) = GivenRequest();
        note.Version = 3;

        await Handler.Handle(Keep(request), CancellationToken.None);

        request.Status.Should().Be(NoteDeletionRequestStatus.Declined);
    }

    [TestCase(NoteDeletionRequestStatus.Approved, "* already deleted this note, Sunday 14 June at 13:00.")]
    [TestCase(NoteDeletionRequestStatus.Declined, "* already decided to keep this note, Sunday 14 June at 13:00.")]
    [TestCase(NoteDeletionRequestStatus.Closed, "This request was closed without a decision*")]
    public async Task Handle_AlreadyDecided_ThrowsConflictSayingHow(NoteDeletionRequestStatus status, string message)
    {
        var (_, note, request) = GivenRequest();
        request.Close(status, OtherUserId, OtherUsername, Now.AddDays(-1));

        var act = () => Handler.Handle(Delete(request), CancellationToken.None);

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

        var act = () => Handler.Handle(deleteNote ? Delete(request) : Keep(request), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
        request.IsPending.Should().BeTrue();
        note.PendingDeletionRequestId.Should().Be(request.Id);
        VerifyNotSaved();
        _requestSaves.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_RequestNotFound_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new DecideNoteDeletionRequestCommand(Guid.NewGuid(), true, 1, null, null, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_AdminDecidingTheirOwnRequest_TellsNobody()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote(Admin.UserId);
        var request = procedure.RequestDeletion(note, Admin);
        _requests.Setup(r => r.GetForUpdateAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        await Handler.Handle(Delete(request), CancellationToken.None);

        _sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_KeepNote_PassesCancellationToken()
    {
        var (procedure, _, request) = GivenRequest();
        using var cts = new CancellationTokenSource();

        await Handler.Handle(Keep(request), cts.Token);

        _requests.Verify(r => r.GetForUpdateAsync(request.Id, cts.Token), Times.Once);
        Repository.Verify(r => r.GetForUpdateAsync(procedure.Id, cts.Token), Times.Once);
        _requestSaves.Should().Equal(cts.Token);
        _sender.Verify(s => s.SendAsync(It.IsAny<Notification>(), cts.Token), Times.Once);
    }

    [Test]
    public async Task Handle_DeleteNote_PassesCancellationToken()
    {
        var (procedure, _, request) = GivenRequest();
        using var cts = new CancellationTokenSource();

        await Handler.Handle(Delete(request), cts.Token);

        _requests.Verify(r => r.GetForUpdateAsync(request.Id, cts.Token), Times.Once);
        Repository.Verify(r => r.GetForUpdateAsync(procedure.Id, cts.Token), Times.Once);
        VerifySavedOnce(cts.Token);
        _monitor.Verify(m => m.NotifyAsync(It.IsAny<ProcedureChange>(), cts.Token), Times.Once);
        _sender.Verify(s => s.SendAsync(It.IsAny<Notification>(), cts.Token), Times.Exactly(2));
        Identity.Verify(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), cts.Token), Times.Once);
    }
}
