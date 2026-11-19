using FluentAssertions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Consults.Common;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Consults;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using Moq;
using static MedApp.UnitTests.Common.Constants.ConsultsTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Consults.Common;

[TestFixture]
public sealed class ConsultNotificationsTests
{
    private readonly Guid _labColleague = Guid.Parse("0000000a-0000-0000-0000-0000000000c4");
    private readonly Guid _labReader = Guid.Parse("0000000a-0000-0000-0000-0000000000c5");
    private readonly CancellationToken _token = new CancellationTokenSource().Token;
    private List<Notification> _sent = null!;
    private ConsultNotifications _notifications = null!;

    [SetUp]
    public void SetUp()
    {
        _sent = [];
        var teams = new Mock<ITeamRepository>();
        teams.Setup(t => t.GetMemberIdsAsync(Lab.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Pathologist.UserId, _labColleague, _labReader]);
        var holders = new Mock<IPermissionHolders>();
        holders.Setup(h => h.WhoHoldsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), Permission.WorkPathways, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { Pathologist.UserId, _labColleague });
        var sender = new Mock<INotificationSender>();
        sender.Setup(s => s.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback((Notification n, CancellationToken _) => _sent.Add(n))
            .Returns(Task.CompletedTask);
        _notifications = new ConsultNotifications(teams.Object, holders.Object, sender.Object);
    }

    [Test]
    public async Task RequestedAsync_TellsTheAskedTeamsMembersWhoWorkPathways_NeverTheQuestion()
    {
        var pathway = CreateDermatologyPathway();
        var change = new ConsultChange(CreateConsult(pathway), pathway, Dermatology.Name, Lab.Name, Asker, Now);

        await _notifications.RequestedAsync(change, Dermatology.Name, _token);

        _sent.Select(n => n.UserId).Should().BeEquivalentTo([Pathologist.UserId, _labColleague]);
        _sent.Should().AllSatisfy(n =>
        {
            n.Kind.Should().Be(NotificationKind.ConsultRequested);
            n.Title.Should().Be("Consult requested");
            n.Message.Should().Be("test.staff from Dermatology asked your team for a consult.");
            n.PathwayId.Should().Be(pathway.Id);
            n.CreatedAtUtc.Should().Be(Now);
        });
    }

    [Test]
    public async Task RepliedAsync_TellsTheAskerAndAssigneesButNotTheReplier()
    {
        var pathway = CreateDermatologyPathway();
        pathway.Assignments.Add(new PatientPathwayAssignment { PatientPathwayId = pathway.Id, UserId = OtherUserId });
        pathway.Assignments.Add(new PatientPathwayAssignment { PatientPathwayId = pathway.Id, UserId = Pathologist.UserId });
        var consult = CreateConsult(pathway, ConsultStatus.Replied);
        var change = new ConsultChange(consult, pathway, Dermatology.Name, Lab.Name, Pathologist, Now);

        await _notifications.RepliedAsync(change, Lab.Name, _token);

        _sent.Select(n => n.UserId).Should().BeEquivalentTo([Asker.UserId, OtherUserId]);
        _sent.Should().AllSatisfy(n =>
        {
            n.Kind.Should().Be(NotificationKind.ConsultReplied);
            n.Title.Should().Be("Consult replied");
            n.Message.Should().Be("test.pathologist from Lab replied to your team’s consult.").And.NotContain(Reply);
        });
    }

    [TestCase(NotificationKind.ConsultRequested, 49)]
    [TestCase(NotificationKind.ConsultReplied, 50)]
    public void Kinds_KeepTheirStoredNumbersAndFixedTitles(NotificationKind kind, int number)
    {
        ((int)kind).Should().Be(number);
        Notification.HasFixedTitle(kind).Should().BeTrue();
    }
}

[TestFixture]
public sealed class ConsultChangeTests
{
    [Test]
    public void Requested_OpensAtVersionOneWithTheGrant()
    {
        var pathway = CreateDermatologyPathway();
        var consult = CreateConsult(pathway);
        var change = new ConsultChange(consult, pathway, Dermatology.Name, Lab.Name, Asker, Now);

        change.Requested();

        consult.Version.Should().Be(1);
        consult.Status.Should().Be(ConsultStatus.Open);
        change.NewGrant!.Reason.Should().Be(MedApp.Domain.Teams.PathwayAccessReason.Consult);
        change.NewGrant.TeamId.Should().Be(Lab.Id);
        change.EndsGrant.Should().BeFalse();
        var entry = change.AuditEntries.Should().ContainSingle().Subject;
        entry.PatientPathwayId.Should().Be(pathway.Id);
        entry.PathwayTitle.Should().Be(PathwayTitle);
        entry.PatientId.Should().Be(pathway.PatientId);
        entry.UserId.Should().Be(Asker.UserId);
        entry.Username.Should().Be(Asker.Username);
        entry.AtUtc.Should().Be(Now);
        entry.Details.Should().Be(
            $"{{\"consultId\":\"{consult.Id}\",\"askingTeamId\":\"{Dermatology.Id}\",\"askingTeam\":\"Dermatology\"," +
            $"\"askedTeamId\":\"{Lab.Id}\",\"askedTeam\":\"Lab\"}}");
    }

    [Test]
    public void ClosedAndWithdrawn_EndTheGrantAndMoveTheVersion()
    {
        var pathway = CreateDermatologyPathway();
        var replied = CreateConsult(pathway, ConsultStatus.Replied);
        var open = CreateConsult(pathway);
        var close = new ConsultChange(replied, pathway, Dermatology.Name, Lab.Name, Asker, Now);
        var withdraw = new ConsultChange(open, pathway, Dermatology.Name, Lab.Name, Asker, Now);

        close.Closed();
        withdraw.Withdrawn();

        replied.Version.Should().Be(3);
        open.Version.Should().Be(2);
        close.EndsGrant.Should().BeTrue();
        withdraw.EndsGrant.Should().BeTrue();
        close.NewGrant.Should().BeNull();
        open.ClosedAtUtc.Should().Be(Now);
        withdraw.AuditEntries.Single().Action.Should().Be(PatientPathwayAuditAction.ConsultWithdrawn);
    }
}
