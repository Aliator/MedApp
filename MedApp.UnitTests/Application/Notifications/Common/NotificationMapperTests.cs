using FluentAssertions;
using MedApp.Application.Notifications.Common;
using static MedApp.UnitTests.Common.Constants.NotificationsTestConstants;
using MedApp.Domain.Notifications;

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

    [TestCase(NotificationKind.AppointmentBooked, "AppointmentBooked")]
    [TestCase(NotificationKind.AppointmentRescheduled, "AppointmentRescheduled")]
    [TestCase(NotificationKind.AppointmentCancelled, "AppointmentCancelled")]
    public void ToResponse_AppointmentNotification_CarriesTheAppointmentAndItsKindByName(NotificationKind kind, string expected)
    {
        var notification = new Notification
        {
            Id = NotificationId,
            UserId = UserId,
            Kind = kind,
            Title = "Appointment",
            Message = Message,
            AppointmentId = AppointmentId,
            CreatedAtUtc = CreatedAtUtc,
        };

        var response = NotificationMapper.ToResponse(notification);

        response.Kind.Should().Be(expected);
        response.AppointmentId.Should().Be(AppointmentId);
        response.ProcedureId.Should().BeNull();
    }

    [Test]
    public void ToResponse_ProcedureNotification_HasNoAppointment()
    {
        NotificationMapper.ToResponse(CreateNotification()).AppointmentId.Should().BeNull();
    }
}
