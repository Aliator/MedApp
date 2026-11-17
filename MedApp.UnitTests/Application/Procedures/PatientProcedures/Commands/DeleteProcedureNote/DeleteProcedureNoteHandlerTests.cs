using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.PatientProcedures.Commands.DeleteProcedureNote;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Notifications;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Commands.DeleteProcedureNote;

[TestFixture]
public sealed class DeleteProcedureNoteHandlerTests : ProcedureHandlerTestBase
{
    private Mock<INoteDeletionRequestRepository> _requests = null!;
    private Mock<IProcedureMonitor> _monitor = null!;
    private List<Notification> _sent = null!;

    [SetUp]
    public void SetUp()
    {
        _requests = new Mock<INoteDeletionRequestRepository>();
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

    private DeleteProcedureNoteHandler Handler => Fixture.Create<DeleteProcedureNoteHandler>();

    [Test]
    public async Task Handle_Note_MarksItDeletedAndAuditsItsIdVersionAndReasonOnly()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote(OtherUserId, version: 2);

        await Handler.Handle(new DeleteProcedureNoteCommand(procedure.Id, note.Id, $"  {DeletionReason}  ", Admin), CancellationToken.None);

        note.Should().BeEquivalentTo(new
        {
            DeletedAtUtc = (DateTime?)Now,
            DeletedByUserId = (Guid?)Admin.UserId,
            DeletionReason,
            Text = NoteText,
            Version = 2
        });
        procedure.Notes.Should().Contain(note, "deleting is soft: the note is kept");
        var change = SavedChanges.Should().ContainSingle().Subject;
        change.DeletedNotes.Should().Equal(note);
        change.NoteVersions.Should().BeEmpty();
        var entry = SavedAudit.Should().ContainSingle().Subject;
        entry.Action.Should().Be(PatientProcedureAuditAction.NoteDeleted);
        entry.Details.Should().Be($$"""{"noteId":"{{note.Id}}","version":2,"reason":"{{DeletionReason}}"}""").And.NotContain(NoteText);
        _monitor.Verify(m => m.NotifyAsync(change, CancellationToken.None), Times.Once);
        _sent.Should().BeEmpty();
        VerifySavedOnce();
    }

    [TestCase(Status.Cancelled)]
    [TestCase(Status.Archived)]
    [TestCase(Status.Completed)]
    public async Task Handle_ProcedureClosed_StillDeletes(Status status)
    {
        // A correction, like an admin deleting a file: allowed whatever the procedure's status.
        var procedure = GivenProcedure(CreateProcedure(status));
        var note = procedure.AddNote();

        await Handler.Handle(new DeleteProcedureNoteCommand(procedure.Id, note.Id, DeletionReason, Admin), CancellationToken.None);

        note.IsDeleted.Should().BeTrue();
        VerifySavedOnce();
    }

    [Test]
    public async Task Handle_PatientRestricted_ThrowsConflictWithoutSaving()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.RestrictedAtUtc = Now.AddDays(-1);
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress, patient: patient));
        var note = procedure.AddNote();

        var act = () => Handler.Handle(new DeleteProcedureNoteCommand(procedure.Id, note.Id, DeletionReason, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        note.IsDeleted.Should().BeFalse();
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_PatientAnonymised_ThrowsConflict()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.AnonymisedAtUtc = Now.AddDays(-1);
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress, patient: patient));
        var note = procedure.AddNote();

        var act = () => Handler.Handle(new DeleteProcedureNoteCommand(procedure.Id, note.Id, DeletionReason, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_AlreadyDeleted_ThrowsConflict()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote();
        note.DeletedAtUtc = Now.AddDays(-1);

        var act = () => Handler.Handle(new DeleteProcedureNoteCommand(procedure.Id, note.Id, DeletionReason, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(ProcedureNotes.DeletedMessage);
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_NoteNotOnTheProcedure_ThrowsNotFound()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));

        var act = () => Handler.Handle(new DeleteProcedureNoteCommand(procedure.Id, Guid.NewGuid(), DeletionReason, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_RequestWaiting_ApprovesItAndTellsTheRequester()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote(OtherUserId);
        var request = procedure.RequestDeletion(note);
        _requests.Setup(r => r.GetForUpdateAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        await Handler.Handle(new DeleteProcedureNoteCommand(procedure.Id, note.Id, DeletionReason, Admin), CancellationToken.None);

        request.Should().BeEquivalentTo(new
        {
            Status = NoteDeletionRequestStatus.Approved,
            DecidedByUserId = (Guid?)Admin.UserId,
            DecidedByUsername = Admin.Username,
            DecidedAtUtc = (DateTime?)Now
        });
        note.PendingDeletionRequestId.Should().BeNull();
        SavedAudit.Single().Details.Should().Contain($"\"requestId\":\"{request.Id}\"");
        _sent.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            UserId = Staff.UserId,
            Kind = NotificationKind.NoteDeletionApproved,
            ProcedureId = (Guid?)procedure.Id
        });
    }

    [Test]
    public async Task Handle_PassesCancellationToken()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote();
        var request = procedure.RequestDeletion(note);
        _requests.Setup(r => r.GetForUpdateAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        using var cts = new CancellationTokenSource();

        await Handler.Handle(new DeleteProcedureNoteCommand(procedure.Id, note.Id, DeletionReason, Admin), cts.Token);

        Repository.Verify(r => r.GetForUpdateAsync(procedure.Id, cts.Token), Times.Once);
        _requests.Verify(r => r.GetForUpdateAsync(request.Id, cts.Token), Times.Once);
        _monitor.Verify(m => m.NotifyAsync(It.IsAny<ProcedureChange>(), cts.Token), Times.Once);
        VerifySavedOnce(cts.Token);
    }
}
