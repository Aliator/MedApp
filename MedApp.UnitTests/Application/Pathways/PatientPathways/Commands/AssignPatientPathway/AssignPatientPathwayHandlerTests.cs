using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;
using MedApp.Application.Pathways.PatientPathways.Commands.AssignPatientPathway;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.AssignPatientPathway;

[TestFixture]
public sealed class AssignPatientPathwayHandlerTests : PathwayHandlerTestBase
{
    private Mock<INotificationSender> _notifications = null!;

    private AssignPatientPathwayHandler Handler => Fixture.Create<AssignPatientPathwayHandler>();

    [SetUp]
    public void SetUpNotifications()
    {
        _notifications = new Mock<INotificationSender>();
        Fixture.Inject(_notifications.Object);
    }

    [Test]
    public async Task Handle_UserDoesNotExist_ThrowsNotFound()
    {
        var pathway = GivenPathway(CreatePathway(Status.Unassigned));

        var act = () => Handler.Handle(new AssignPatientPathwayCommand(pathway.Id, Guid.NewGuid(), Admin), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_AlreadyAssigned_ThrowsConflict()
    {
        var pathway = GivenPathway(CreatePathway(Status.Assigned));

        var act = () => Handler.Handle(new AssignPatientPathwayCommand(pathway.Id, Staff.UserId, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"{Staff.Username} is already assigned*");
    }

    [Test]
    public async Task Handle_PathwayCompleted_ThrowsConflict()
    {
        var pathway = GivenPathway(CreatePathway(Status.Completed, finishedStages: 3));

        var act = () => Handler.Handle(new AssignPatientPathwayCommand(pathway.Id, OtherUserId, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Handle_NewAssignee_AssignsThemAndSaves()
    {
        var pathway = GivenPathway(CreatePathway(Status.Unassigned));
        using var cts = new CancellationTokenSource();

        var response = await Handler.Handle(new AssignPatientPathwayCommand(pathway.Id, OtherUserId, Admin), cts.Token);

        response.Status.Should().Be("Assigned");
        response.Assignments.Should().ContainSingle(a => a.Username == OtherUsername && a.AssignedByUsername == Admin.Username);
        SavedAudit.Select(e => e.Action).Should().Contain(PatientPathwayAuditAction.Assigned);
        VerifySavedOnce(cts.Token);
    }

    [Test]
    public async Task Handle_NewAssignee_NotifiesThem()
    {
        var pathway = GivenPathway(CreatePathway(Status.Unassigned));
        using var cts = new CancellationTokenSource();

        await Handler.Handle(new AssignPatientPathwayCommand(pathway.Id, OtherUserId, Admin), cts.Token);

        _notifications.Verify(n => n.SendAsync(
            It.Is<Notification>(x =>
                x.UserId == OtherUserId
                && x.Kind == NotificationKind.PathwayAssigned
                && x.PathwayId == pathway.Id
                && x.Title == pathway.Title
                && x.Message == $"{Admin.Username} assigned you to this pathway."
                && x.CreatedAtUtc == Now
                && x.ReadAtUtc == null),
            cts.Token), Times.Once);
    }

    [Test]
    public async Task Handle_NewAssignee_TellsThemBeforeTheWatchingAdmins()
    {
        var pathway = GivenPathway(CreatePathway(Status.Unassigned));
        var order = new List<string>();
        var monitor = new Mock<IPathwayMonitor>();
        _notifications.Setup(n => n.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("assignee")).Returns(Task.CompletedTask);
        monitor.Setup(m => m.NotifyAsync(It.IsAny<PathwayChange>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("admins")).Returns(Task.CompletedTask);
        Fixture.Inject(monitor.Object);

        await Handler.Handle(new AssignPatientPathwayCommand(pathway.Id, OtherUserId, Admin), CancellationToken.None);

        order.Should().Equal("assignee", "admins");
    }

    [Test]
    public async Task Handle_AssigningYourself_SendsNoNotification()
    {
        var pathway = GivenPathway(CreatePathway(Status.Unassigned));

        await Handler.Handle(new AssignPatientPathwayCommand(pathway.Id, Admin.UserId, Admin), CancellationToken.None);

        _notifications.Verify(n => n.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
