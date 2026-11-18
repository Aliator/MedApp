using FluentAssertions;
using MedApp.Application.Notifications.Common;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Domain.Notifications;
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

    [TestCase(NotificationKind.MeetingInvited, "MeetingInvited")]
    [TestCase(NotificationKind.MeetingChanged, "MeetingChanged")]
    [TestCase(NotificationKind.MeetingCancelled, "MeetingCancelled")]
    [TestCase(NotificationKind.MeetingDeclined, "MeetingDeclined")]
    public void ToResponse_MeetingNotification_CarriesItsCalendarDateAndKindByName(NotificationKind kind, string expected)
    {
        var date = new DateOnly(2026, 11, 23);
        var notification = new Notification
        {
            Id = NotificationId,
            UserId = UserId,
            Kind = kind,
            Title = "Meeting invitation",
            Message = Message,
            CalendarDate = date,
            MeetingId = MeetingId,
            CreatedAtUtc = CreatedAtUtc,
        };

        var response = NotificationMapper.ToResponse(notification);

        response.Kind.Should().Be(expected);
        response.CalendarDate.Should().Be(date);
        response.MeetingId.Should().Be(MeetingId);
        response.ProcedureId.Should().BeNull();
        response.AppointmentId.Should().BeNull();
    }

    private static readonly Guid MeetingId = Guid.Parse("0000000d-0000-0000-0000-000000000001");

    [Test]
    public void ToResponse_NoteDeletionRequested_CarriesTheRequestAndProcedure()
    {
        var requestId = Guid.NewGuid();
        var notification = new Notification
        {
            Id = NotificationId,
            UserId = UserId,
            Kind = NotificationKind.NoteDeletionRequested,
            Title = "Note deletion requested",
            Message = "test.staff asked to delete a note on a procedure.",
            ProcedureId = ProcedureId,
            NoteDeletionRequestId = requestId
        };

        NotificationMapper.ToResponse(notification).Should().BeEquivalentTo(new
        {
            Kind = "NoteDeletionRequested",
            ProcedureId = (Guid?)ProcedureId,
            NoteDeletionRequestId = (Guid?)requestId,
            AppointmentId = (Guid?)null
        });
    }

    [Test]
    public void ToResponse_ProcedureNotification_HasNoCalendarDateOrRequest()
    {
        NotificationMapper.ToResponse(CreateNotification()).NoteDeletionRequestId.Should().BeNull();
    }

    [Test]
    public void ToResponse_ProcedureNotification_HasNoCalendarDate()
    {
        NotificationMapper.ToResponse(CreateNotification()).MeetingId.Should().BeNull();
        NotificationMapper.ToResponse(CreateNotification()).CalendarDate.Should().BeNull();
    }

    [Test]
    public void ToResponse_MonitoringKind_GivesItsName()
    {
        var notification = new Notification { Id = NotificationId, UserId = UserId, Kind = NotificationKind.MonitoredStageFinished };

        NotificationMapper.ToResponse(notification).Kind.Should().Be("MonitoredStageFinished");
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ToResponse_Preferences_GivesTheirChoice(bool procedureAssigned)
    {
        var preferences = new NotificationPreferences { UserId = UserId, ProcedureAssigned = procedureAssigned };

        NotificationMapper.ToResponse(preferences).Should().Be(new NotificationPreferencesResponse(procedureAssigned));
    }

    [Test]
    public void ToResponse_DefaultPreferences_GetEverything()
    {
        NotificationMapper.ToResponse(NotificationPreferences.Defaults(UserId)).Should().Be(new NotificationPreferencesResponse(true));
    }
}
