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
    private Mock<INotificationSender> _sender = null!;
    private List<Notification> _sent = null!;

    [SetUp]
    public void SetUp()
    {
        _requests = new Mock<INoteDeletionRequestRepository>();
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

    private DeleteProcedureNoteHandler Handler => Fixture.Create<DeleteProcedureNoteHandler>();

    private static DeleteProcedureNoteCommand Command(
        PatientProcedure procedure, PatientProcedureNote note, string reason = "WrongPatient", string? detail = DeletionDetail, int? version = null) =>
        new(procedure.Id, note.Id, reason, detail, version ?? note.Version, Admin);

    [Test]
    public async Task Handle_Note_MarksItDeletedAndAuditsItsIdVersionAndReasonOnly()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote(OtherUserId, version: 2);

        await Handler.Handle(Command(procedure, note, detail: $"  {DeletionDetail}  "), CancellationToken.None);

        note.Should().BeEquivalentTo(new
        {
            DeletedAtUtc = (DateTime?)Now,
            DeletedByUserId = (Guid?)Admin.UserId,
            DeletionReason = (NoteDeletionReason?)NoteDeletionReason.WrongPatient,
            DeletionDetail,
            Text = NoteText,
            Version = 2
        });
        procedure.Notes.Should().Contain(note, "deleting is soft: the note is kept");
        var change = SavedChanges.Should().ContainSingle().Subject;
        change.DeletedNotes.Should().Equal(note);
        change.NoteVersions.Should().BeEmpty();
        var entry = SavedAudit.Should().ContainSingle().Subject;
        entry.Action.Should().Be(PatientProcedureAuditAction.NoteDeleted);
        entry.Details.Should().Be($$"""{"noteId":"{{note.Id}}","version":2,"deletionReason":"WrongPatient","detail":"{{DeletionDetail}}"}""")
            .And.NotContain(NoteText);
        _monitor.Verify(m => m.NotifyAsync(change, CancellationToken.None), Times.Once);
        VerifySavedOnce();
    }

    [Test]
    public async Task Handle_NoDetail_RecordsTheReasonAlone()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote();

        await Handler.Handle(Command(procedure, note, reason: "duplicate", detail: "  "), CancellationToken.None);

        note.DeletionReason.Should().Be(NoteDeletionReason.Duplicate);
        note.DeletionDetail.Should().BeNull();
    }

    [Test]
    public async Task Handle_EditedSinceTheAdminOpenedIt_ThrowsConflictWithoutSaving()
    {
        // The admin read version 1; the author has saved version 2 since.
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote(version: 2);

        var act = () => Handler.Handle(Command(procedure, note, version: 1), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(ProcedureNotes.EditedSinceOpenedMessage);
        note.IsDeleted.Should().BeFalse();
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_SomeoneElsesNote_TellsItsAuthor()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote(OtherUserId);

        await Handler.Handle(Command(procedure, note), CancellationToken.None);

        _sent.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            UserId = OtherUserId,
            Kind = NotificationKind.AuthorsNoteDeleted,
            Title = "Your note was deleted",
            Message = $"{Admin.Username} deleted a note you wrote on a procedure.",
            ProcedureId = (Guid?)procedure.Id
        });
    }

    [Test]
    public async Task Handle_TheirOwnNote_TellsNobody()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote(Admin.UserId);

        await Handler.Handle(Command(procedure, note), CancellationToken.None);

        _sent.Should().BeEmpty();
    }

    [TestCase(Status.Cancelled)]
    [TestCase(Status.Archived)]
    [TestCase(Status.Completed)]
    public async Task Handle_ProcedureClosed_StillDeletes(Status status)
    {
        // A correction, like an admin deleting a file: allowed whatever the procedure's status.
        var procedure = GivenProcedure(CreateProcedure(status));
        var note = procedure.AddNote();

        await Handler.Handle(Command(procedure, note), CancellationToken.None);

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

        var act = () => Handler.Handle(Command(procedure, note), CancellationToken.None);

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

        var act = () => Handler.Handle(Command(procedure, note), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_AlreadyDeleted_ThrowsConflict()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote();
        note.DeletedAtUtc = Now.AddDays(-1);

        var act = () => Handler.Handle(Command(procedure, note), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(ProcedureNotes.DeletedMessage);
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_NoteNotOnTheProcedure_ThrowsNotFound()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));

        var act = () => Handler.Handle(new DeleteProcedureNoteCommand(procedure.Id, Guid.NewGuid(), "WrongPatient", null, 1, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_RequestWaiting_ApprovesItAndTellsTheRequesterAndTheAuthor()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote(OtherUserId);
        var request = procedure.RequestDeletion(note);
        _requests.Setup(r => r.GetForUpdateAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        await Handler.Handle(Command(procedure, note), CancellationToken.None);

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
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote(Staff.UserId);
        var request = procedure.RequestDeletion(note);
        _requests.Setup(r => r.GetForUpdateAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        await Handler.Handle(Command(procedure, note), CancellationToken.None);

        _sent.Should().ContainSingle().Which.Kind.Should().Be(NotificationKind.NoteDeletionApproved);
    }

    [Test]
    public async Task Handle_PassesCancellationToken()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote(OtherUserId);
        var request = procedure.RequestDeletion(note);
        _requests.Setup(r => r.GetForUpdateAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        using var cts = new CancellationTokenSource();

        await Handler.Handle(Command(procedure, note), cts.Token);

        Repository.Verify(r => r.GetForUpdateAsync(procedure.Id, cts.Token), Times.Once);
        _requests.Verify(r => r.GetForUpdateAsync(request.Id, cts.Token), Times.Once);
        _monitor.Verify(m => m.NotifyAsync(It.IsAny<ProcedureChange>(), cts.Token), Times.Once);
        _sender.Verify(s => s.SendAsync(It.IsAny<Notification>(), cts.Token), Times.Exactly(2));
        VerifySavedOnce(cts.Token);
    }
}
