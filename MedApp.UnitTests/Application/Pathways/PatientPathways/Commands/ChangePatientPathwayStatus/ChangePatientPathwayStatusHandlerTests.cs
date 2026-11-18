using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;
using MedApp.Application.Pathways.PatientPathways.Commands.ChangePatientPathwayStatus;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Appointments;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.ChangePatientPathwayStatus;

[TestFixture]
public sealed class ChangePatientPathwayStatusHandlerTests : PathwayHandlerTestBase
{
    private ChangePatientPathwayStatusHandler Handler => Fixture.Create<ChangePatientPathwayStatusHandler>();

    [Test]
    public async Task Handle_ChangeNotAllowed_ThrowsConflict()
    {
        var pathway = GivenPathway(CreatePathway(Status.Assigned));

        var act = () => Handler.Handle(new ChangePatientPathwayStatusCommand(pathway.Id, "Completed", null, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*assigned*discharged*");
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_Block_BlocksWithTheReasonAndSaves()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress, finishedStages: 1));

        var response = await Handler.Handle(
            new ChangePatientPathwayStatusCommand(pathway.Id, "blocked", $"  {Reason} ", Staff), CancellationToken.None);

        response.Status.Should().Be("Blocked");
        response.BlockedFromStatus.Should().Be("InProgress");
        response.AllowedStatusChanges.Should().Equal("InProgress", "Cancelled");
        SavedAudit.Single().Details.Should().Contain($"\"reason\":\"{Reason}\"");
        VerifySavedOnce();
    }

    [Test]
    public async Task Handle_CompleteFromReview_Completes()
    {
        var pathway = GivenPathway(CreatePathway(Status.InReview, finishedStages: 3));

        var response = await Handler.Handle(new ChangePatientPathwayStatusCommand(pathway.Id, "Completed", null, Staff), CancellationToken.None);

        response.Status.Should().Be("Completed");
        response.AllowedStatusChanges.Should().Equal("Archived");
    }

    [Test]
    public async Task Handle_CompleteWithAStageReopened_ThrowsConflict()
    {
        var pathway = GivenPathway(CreatePathway(Status.InReview, finishedStages: 3));
        pathway.StageAt(2).IsLocked = false;

        var act = () => Handler.Handle(new ChangePatientPathwayStatusCommand(pathway.Id, "Completed", null, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Handle_Cancel_CancelsThePathwaysRequestedVisitsButNotBookedOnes()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress, finishedStages: 1));
        var requested = CreateStageAppointment(pathway, pathway.StageAt(2));
        var booked = CreateStageAppointment(pathway, pathway.StageAt(3), AppointmentStatus.Booked);
        Appointments
            .Setup(r => r.GetRequestedForPathwayForUpdateAsync(pathway.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([requested, booked]);

        var response = await Handler.Handle(
            new ChangePatientPathwayStatusCommand(pathway.Id, "Cancelled", Reason, Staff), CancellationToken.None);

        response.Status.Should().Be("Cancelled");
        requested.Status.Should().Be(AppointmentStatus.Cancelled);
        requested.CancelReason.Should().Be(PathwayChange.PathwayCancelledReason);
        booked.Status.Should().Be(AppointmentStatus.Booked);
        var change = SavedChanges.Should().ContainSingle().Subject;
        change.AppointmentEvents.Should().ContainSingle(e => e.AppointmentId == requested.Id && e.Action == AppointmentEventAction.Cancelled);
        change.AuditEntries.Should().ContainSingle(e => e.Action == PatientPathwayAuditAction.StatusChanged);
    }

    [Test]
    public async Task Handle_Cancel_PassesTheTokenToTheAppointmentsAndTheSave()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        using var cts = new CancellationTokenSource();

        await Handler.Handle(new ChangePatientPathwayStatusCommand(pathway.Id, "Cancelled", null, Staff), cts.Token);

        Appointments.Verify(r => r.GetRequestedForPathwayForUpdateAsync(pathway.Id, cts.Token), Times.Once);
        VerifySavedOnce(cts.Token);
    }

    [Test]
    public async Task Handle_NotCancelling_LeavesTheAppointmentsAlone()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));

        await Handler.Handle(new ChangePatientPathwayStatusCommand(pathway.Id, "Blocked", Reason, Staff), CancellationToken.None);

        Appointments.Verify(
            r => r.GetRequestedForPathwayForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        SavedChanges.Single().AppointmentEvents.Should().BeEmpty();
    }
}
