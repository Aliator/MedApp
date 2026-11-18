using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;
using MedApp.Application.Procedures.PatientProcedures.Commands.DeletePatientProcedure;
using MedApp.Application.Procedures.Repositories;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Commands.DeletePatientProcedure;

[TestFixture]
public sealed class DeletePatientProcedureHandlerTests : ProcedureHandlerTestBase
{
    private DeletePatientProcedureHandler Handler => Fixture.Create<DeletePatientProcedureHandler>();

    [Test]
    public async Task Handle_ProcedureDoesNotExist_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new DeletePatientProcedureCommand(Guid.NewGuid(), Admin), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [TestCase(Status.InProgress)]
    [TestCase(Status.Completed)]
    public async Task Handle_ProcedureNotCancelledOrArchived_ThrowsConflict(Status status)
    {
        var procedure = GivenProcedure(CreateProcedure(status));

        var act = () => Handler.Handle(new DeletePatientProcedureCommand(procedure.Id, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        Repository.Verify(r => r.DeleteAsync(It.IsAny<PatientProcedure>(), It.IsAny<IEnumerable<PatientProcedureAuditEntry>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_CancelledProcedure_DeletesItAndAuditsTheDeletion()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.Cancelled));
        using var cts = new CancellationTokenSource();

        await Handler.Handle(new DeletePatientProcedureCommand(procedure.Id, Admin), cts.Token);

        Repository.Verify(r => r.DeleteAsync(procedure, It.IsAny<IEnumerable<PatientProcedureAuditEntry>>(), cts.Token), Times.Once);
        SavedAudit.Should().ContainSingle(e => e.Action == PatientProcedureAuditAction.Deleted && e.UserId == Admin.UserId);
        Attachments.Verify(a => a.CountForProcedureAsync(procedure.Id, cts.Token), Times.Once);
    }

    [Test]
    public async Task Handle_RequestsToDeleteItsNotesWaiting_ClosesThemUndecidedByTheAdmin()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.Cancelled));
        var waiting = procedure.RequestDeletion(procedure.AddNote());
        var requests = new Mock<INoteDeletionRequestRepository>();
        requests.Setup(r => r.GetPendingForProcedureForUpdateAsync(procedure.Id, It.IsAny<CancellationToken>())).ReturnsAsync([waiting]);
        Fixture.Inject(requests.Object);
        using var cts = new CancellationTokenSource();

        await Handler.Handle(new DeletePatientProcedureCommand(procedure.Id, Admin), cts.Token);

        waiting.Should().BeEquivalentTo(new
        {
            Status = NoteDeletionRequestStatus.Closed,
            DecidedByUserId = (Guid?)Admin.UserId,
            DecidedByUsername = Admin.Username,
            DecidedAtUtc = (DateTime?)Now
        });
        requests.Verify(r => r.GetPendingForProcedureForUpdateAsync(procedure.Id, cts.Token), Times.Once);
        requests.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never, "the deletion's save saves them");
    }

    [TestCase(1, "This procedure has 1 file attached*")]
    [TestCase(3, "This procedure has 3 files attached*")]
    public async Task Handle_ProcedureHasFiles_ThrowsConflictAndKeepsIt(int files, string message)
    {
        var procedure = GivenProcedure(CreateProcedure(Status.Cancelled));
        Attachments.Setup(a => a.CountForProcedureAsync(procedure.Id, It.IsAny<CancellationToken>())).ReturnsAsync(files);

        var act = () => Handler.Handle(new DeletePatientProcedureCommand(procedure.Id, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(message);
        Repository.Verify(r => r.DeleteAsync(It.IsAny<PatientProcedure>(), It.IsAny<IEnumerable<PatientProcedureAuditEntry>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
