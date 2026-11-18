using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Notifications;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Common;

[TestFixture]
public sealed class ClosedDayBookingsTests
{
    [TestCase(0, 0, 0, "nothing")]
    [TestCase(1, 0, 0, "an appointment")]
    [TestCase(0, 1, 0, "a block")]
    [TestCase(0, 0, 1, "a meeting")]
    [TestCase(3, 0, 0, "3 appointments")]
    [TestCase(2, 1, 0, "2 appointments and a block")]
    [TestCase(2, 2, 3, "2 appointments, 2 blocks and 3 meetings")]
    public void What_PutsTheCountsInWords(int appointments, int blocks, int meetings, string words)
    {
        ClosedDayBookings.What(new StaffBookings(StaffUserId, appointments, blocks, meetings)).Should().Be(words);
    }

    [Test]
    public void Notifications_OnePerMemberOfStaff_ButTheAdmin_WithAFixedTitleAndNoPatient()
    {
        var notifications = ClosedDayBookings.Notifications(
            ClosedWednesday,
            ClosedDayName,
            [new StaffBookings(StaffUserId, 1, 0, 0), new StaffBookings(AdminUserId, 0, 0, 1)],
            AdminActor,
            Now);

        var notification = notifications.Should().ContainSingle().Subject;
        notification.Id.Should().NotBeEmpty();
        notification.UserId.Should().Be(StaffUserId);
        notification.Kind.Should().Be(NotificationKind.DayClosed);
        notification.Title.Should().Be(ClosedDayBookings.NotificationTitle);
        notification.Message.Should().Be(
            $"{AdminUsername} closed Wednesday 25 November ({ClosedDayName}). You have an appointment then. Move or cancel what's still to come, and tell any patients.");
        notification.CalendarDate.Should().Be(ClosedWednesday);
        notification.CreatedAtUtc.Should().Be(Now);
        Notification.HasFixedTitle(NotificationKind.DayClosed).Should().BeTrue();
    }

    [Test]
    public async Task ToResponseAsync_TotalsAndEachPersonByUsername()
    {
        var identity = new Mock<IIdentityReadService>();
        using var cts = new CancellationTokenSource();
        identity.Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), cts.Token))
            .ReturnsAsync(new Dictionary<Guid, string> { [StaffUserId] = "zed.staff", [OtherStaffUserId] = "amy.staff" });

        var response = await ClosedDayBookings.ToResponseAsync(
            ClosedWednesday,
            [new StaffBookings(StaffUserId, 2, 1, 0), new StaffBookings(OtherStaffUserId, 0, 0, 1)],
            identity.Object,
            cts.Token);

        response.Should().BeEquivalentTo(new { Date = ClosedWednesday, Appointments = 2, Blocks = 1, Meetings = 1 });
        response.Staff.Select(s => (s.Username, s.Appointments, s.Blocks, s.Meetings))
            .Should().Equal(("amy.staff", 0, 0, 1), ("zed.staff", 2, 1, 0));
    }
}
