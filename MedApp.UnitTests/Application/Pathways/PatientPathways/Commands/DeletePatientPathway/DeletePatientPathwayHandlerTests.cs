using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;
using MedApp.Application.Pathways.PatientPathways.Commands.DeletePatientPathway;
using MedApp.Application.Pathways.Repositories;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.DeletePatientPathway;

[TestFixture]
public sealed class DeletePatientPathwayHandlerTests : PathwayHandlerTestBase
{
    private DeletePatientPathwayHandler Handler => Fixture.Create<DeletePatientPathwayHandler>();

    [Test]
    public async Task Handle_PathwayDoesNotExist_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new DeletePatientPathwayCommand(Guid.NewGuid(), Admin), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [TestCase(Status.InProgress)]
    [TestCase(Status.Completed)]
    public async Task Handle_PathwayNotCancelledOrArchived_ThrowsConflict(Status status)
    {
        var pathway = GivenPathway(CreatePathway(status));

        var act = () => Handler.Handle(new DeletePatientPathwayCommand(pathway.Id, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        Repository.Verify(r => r.DeleteAsync(It.IsAny<PatientPathway>(), It.IsAny<IEnumerable<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_CancelledPathway_DeletesItAndAuditsTheDeletion()
    {
        var pathway = GivenPathway(CreatePathway(Status.Cancelled));
        using var cts = new CancellationTokenSource();

        await Handler.Handle(new DeletePatientPathwayCommand(pathway.Id, Admin), cts.Token);

        Repository.Verify(r => r.DeleteAsync(pathway, It.IsAny<IEnumerable<PatientPathwayAuditEntry>>(), cts.Token), Times.Once);
        SavedAudit.Should().ContainSingle(e => e.Action == PatientPathwayAuditAction.Deleted && e.UserId == Admin.UserId);
        Attachments.Verify(a => a.CountForPathwayAsync(pathway.Id, cts.Token), Times.Once);
    }

    [Test]
    public async Task Handle_RequestsToDeleteItsNotesWaiting_ClosesThemUndecidedByTheAdmin()
    {
        var pathway = GivenPathway(CreatePathway(Status.Cancelled));
        var waiting = pathway.RequestDeletion(pathway.AddNote());
        var requests = new Mock<INoteDeletionRequestRepository>();
        requests.Setup(r => r.GetPendingForPathwayForUpdateAsync(pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync([waiting]);
        Fixture.Inject(requests.Object);
        using var cts = new CancellationTokenSource();

        await Handler.Handle(new DeletePatientPathwayCommand(pathway.Id, Admin), cts.Token);

        waiting.Should().BeEquivalentTo(new
        {
            Status = NoteDeletionRequestStatus.Closed,
            DecidedByUserId = (Guid?)Admin.UserId,
            DecidedByUsername = Admin.Username,
            DecidedAtUtc = (DateTime?)Now
        });
        requests.Verify(r => r.GetPendingForPathwayForUpdateAsync(pathway.Id, cts.Token), Times.Once);
        requests.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never, "the deletion's save saves them");
    }

    [TestCase(1, "This pathway has 1 file attached*")]
    [TestCase(3, "This pathway has 3 files attached*")]
    public async Task Handle_PathwayHasFiles_ThrowsConflictAndKeepsIt(int files, string message)
    {
        var pathway = GivenPathway(CreatePathway(Status.Cancelled));
        Attachments.Setup(a => a.CountForPathwayAsync(pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(files);

        var act = () => Handler.Handle(new DeletePatientPathwayCommand(pathway.Id, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(message);
        Repository.Verify(r => r.DeleteAsync(It.IsAny<PatientPathway>(), It.IsAny<IEnumerable<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
