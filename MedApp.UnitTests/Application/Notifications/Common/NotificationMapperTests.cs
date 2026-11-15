using FluentAssertions;
using MedApp.Application.Notifications.Common;
using static MedApp.UnitTests.Common.Constants.NotificationsTestConstants;

namespace MedApp.UnitTests.Application.Notifications.Common;

[TestFixture]
public sealed class NotificationMapperTests
{
    [Test]
    public void ToResponse_Notification_ReturnsItWithItsKindByNameAndWhetherRead()
    {
        var unread = NotificationMapper.ToResponse(CreateNotification());
        var read = NotificationMapper.ToResponse(CreateNotification(Now));

        unread.Should().BeEquivalentTo(new
        {
            Id = NotificationId,
            Kind = "ProcedureAssigned",
            Title,
            Message,
            ProcedureId = (Guid?)ProcedureId,
            CreatedAtUtc,
            IsRead = false,
        });
        read.IsRead.Should().BeTrue();
    }

    [Test]
    public void ToResponse_List_ReturnsEachInOrderWithTheUnreadCount()
    {
        var response = NotificationMapper.ToResponse([CreateNotification(), CreateNotification(Now)], unreadCount: 4);

        response.Items.Select(n => n.IsRead).Should().Equal(false, true);
        response.UnreadCount.Should().Be(4);
    }
}
