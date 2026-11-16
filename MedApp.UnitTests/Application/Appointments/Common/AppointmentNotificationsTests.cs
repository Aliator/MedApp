using FluentAssertions;
using MedApp.Application.Appointments.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Notifications;
using static MedApp.UnitTests.Common.Constants.AppointmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Common;

[TestFixture]
public sealed class AppointmentNotificationsTests
{
    private static Appointment BookedAt(DateTime startUtc, Guid? staffUserId = null)
    {
        var appointment = CreateAppointment(AppointmentStatus.Booked, startUtc);
        appointment.StaffUserId = staffUserId ?? StaffUserId;
        appointment.Notes = Notes;
        return appointment;
    }

    [Test]
    public void AfterBooking_RequestedBookedBySomeoneElse_TellsTheStaffMemberItWasBooked()
    {
        var appointment = BookedAt(At(NextMonday, 10));

        var notification = AppointmentNotifications.AfterBooking(
                appointment, ProcedureTitle, AppointmentStatus.Requested, null, null, Colleague, Now)
            .Should().ContainSingle().Subject;

        notification.Id.Should().NotBeEmpty();
        notification.UserId.Should().Be(StaffUserId);
        notification.Kind.Should().Be(NotificationKind.AppointmentBooked);
        notification.Title.Should().Be(ProcedureTitle);
        notification.Message.Should().Be("calendar.colleague booked you an appointment on Monday 23 November at 10:00.");
        notification.ProcedureId.Should().Be(appointment.ProcedureId);
        notification.AppointmentId.Should().Be(appointment.Id);
        notification.CreatedAtUtc.Should().Be(Now);
        notification.ReadAtUtc.Should().BeNull();
    }

    [Test]
    public void AfterBooking_InSummer_DescribesTheUkTime()
    {
        // 09:00 UTC on a BST day is 10:00 UK time.
        var appointment = BookedAt(new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc));

        AppointmentNotifications.AfterBooking(appointment, null, AppointmentStatus.Requested, null, null, Colleague, Now)
            .Single().Message.Should().EndWith("on Monday 15 June at 10:00.");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void AfterBooking_NoProcedureTitle_IsTitledAppointment(string? title)
    {
        AppointmentNotifications.AfterBooking(
                BookedAt(At(NextMonday, 10)), title, AppointmentStatus.Requested, null, null, Colleague, Now)
            .Single().Title.Should().Be(AppointmentNotifications.DefaultTitle);
    }

    [Test]
    public void AfterBooking_BookedByThemselves_TellsNobody()
    {
        AppointmentNotifications.AfterBooking(
                BookedAt(At(NextMonday, 10)), ProcedureTitle, AppointmentStatus.Requested, null, null, Self, Now)
            .Should().BeEmpty();
    }

    [Test]
    public void AfterBooking_NotBooked_TellsNobody()
    {
        AppointmentNotifications.AfterBooking(
                CreateAppointment(AppointmentStatus.Requested), ProcedureTitle, AppointmentStatus.Requested, null, null, Colleague, Now)
            .Should().BeEmpty();
    }

    [Test]
    public void AfterBooking_MovedToAnotherTime_TellsTheStaffMemberItMoved()
    {
        var appointment = BookedAt(At(NextMonday, 15));

        var notification = AppointmentNotifications.AfterBooking(
                appointment, ProcedureTitle, AppointmentStatus.Booked, StaffUserId, At(NextMonday, 10), Colleague, Now)
            .Should().ContainSingle().Subject;

        notification.UserId.Should().Be(StaffUserId);
        notification.Kind.Should().Be(NotificationKind.AppointmentRescheduled);
        notification.Message.Should().Be("calendar.colleague moved your appointment to Monday 23 November at 15:00.");
    }

    [Test]
    public void AfterBooking_SameStaffAndStart_TellsNobody()
    {
        // Only the end moved: the start, which the message gives, is the same.
        AppointmentNotifications.AfterBooking(
                BookedAt(At(NextMonday, 10)), ProcedureTitle, AppointmentStatus.Booked, StaffUserId, At(NextMonday, 10), Colleague, Now)
            .Should().BeEmpty();
    }

    [Test]
    public void AfterBooking_MovedByThemselves_TellsNobody()
    {
        AppointmentNotifications.AfterBooking(
                BookedAt(At(NextMonday, 15)), ProcedureTitle, AppointmentStatus.Booked, StaffUserId, At(NextMonday, 10), Self, Now)
            .Should().BeEmpty();
    }

    [Test]
    public void AfterBooking_MovedToSomeoneElse_TellsTheNewOneItWasBookedAndTheOldOneItWent()
    {
        var appointment = BookedAt(At(NextMonday, 15), OtherStaffUserId);

        var notifications = AppointmentNotifications.AfterBooking(
            appointment, ProcedureTitle, AppointmentStatus.Booked, StaffUserId, At(NextMonday, 10), Colleague, Now);

        notifications.Should().HaveCount(2);
        var booked = notifications.Single(n => n.UserId == OtherStaffUserId);
        booked.Kind.Should().Be(NotificationKind.AppointmentBooked);
        booked.Message.Should().Be("calendar.colleague moved an appointment to you, on Monday 23 November at 15:00.");
        var cancelled = notifications.Single(n => n.UserId == StaffUserId);
        cancelled.Kind.Should().Be(NotificationKind.AppointmentCancelled);
        cancelled.Message.Should().Be("calendar.colleague moved your appointment on Monday 23 November at 10:00 to someone else.");
    }

    [Test]
    public void AfterBooking_MovedByTheOldStaffMemberToSomeoneElse_TellsOnlyTheNewOne()
    {
        var appointment = BookedAt(At(NextMonday, 15), OtherStaffUserId);

        AppointmentNotifications.AfterBooking(
                appointment, ProcedureTitle, AppointmentStatus.Booked, StaffUserId, At(NextMonday, 10), Self, Now)
            .Should().ContainSingle().Which.UserId.Should().Be(OtherStaffUserId);
    }

    [Test]
    public void AfterBooking_MovedByTheNewStaffMemberToThemselves_TellsOnlyTheOldOne()
    {
        var appointment = BookedAt(At(NextMonday, 15), StaffUserId);

        var notification = AppointmentNotifications.AfterBooking(
                appointment, ProcedureTitle, AppointmentStatus.Booked, OtherStaffUserId, At(NextMonday, 10), Self, Now)
            .Should().ContainSingle().Subject;

        notification.UserId.Should().Be(OtherStaffUserId);
        notification.Kind.Should().Be(NotificationKind.AppointmentCancelled);
    }

    [Test]
    public void AfterBooking_PreviousStaffUnknown_TellsOnlyTheNewOne()
    {
        AppointmentNotifications.AfterBooking(
                BookedAt(At(NextMonday, 15)), ProcedureTitle, AppointmentStatus.Booked, null, null, Colleague, Now)
            .Should().ContainSingle().Which.Kind.Should().Be(NotificationKind.AppointmentBooked);
    }

    [Test]
    public void AfterBooking_Messages_NeverHoldThePatientsNameOrNotes()
    {
        var appointment = BookedAt(At(NextMonday, 15), OtherStaffUserId);

        var notifications = AppointmentNotifications.AfterBooking(
            appointment, null, AppointmentStatus.Booked, StaffUserId, At(NextMonday, 10), Colleague, Now);

        notifications.Should().NotBeEmpty();
        foreach (var notification in notifications)
        {
            notification.Message.Should().NotContain(PatientFirstName).And.NotContain(PatientLastName).And.NotContain(Notes);
            notification.Title.Should().NotContain(PatientFirstName).And.NotContain(Notes);
        }
    }

    [Test]
    public void AfterCancelling_BookedWithSomeoneElse_TellsThem()
    {
        var appointment = BookedAt(At(NextMonday, 10));
        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancelReason = CancelReason;

        var notification = AppointmentNotifications.AfterCancelling(appointment, ProcedureTitle, AppointmentStatus.Booked, Colleague, Now)
            .Should().ContainSingle().Subject;

        notification.UserId.Should().Be(StaffUserId);
        notification.Kind.Should().Be(NotificationKind.AppointmentCancelled);
        notification.Title.Should().Be(ProcedureTitle);
        notification.AppointmentId.Should().Be(appointment.Id);
        notification.Message.Should().Be("calendar.colleague cancelled your appointment on Monday 23 November at 10:00.");
        notification.Message.Should().NotContain(CancelReason).And.NotContain(PatientFirstName);
    }

    [Test]
    public void AfterCancelling_NoProcedureTitle_IsTitledAppointment()
    {
        AppointmentNotifications.AfterCancelling(BookedAt(At(NextMonday, 10)), null, AppointmentStatus.Booked, Colleague, Now)
            .Single().Title.Should().Be(AppointmentNotifications.DefaultTitle);
    }

    [Test]
    public void AfterCancelling_TheirOwn_TellsNobody()
    {
        AppointmentNotifications.AfterCancelling(BookedAt(At(NextMonday, 10)), ProcedureTitle, AppointmentStatus.Booked, Self, Now)
            .Should().BeEmpty();
    }

    [Test]
    public void AfterCancelling_WasRequested_TellsNobody()
    {
        AppointmentNotifications.AfterCancelling(
                CreateAppointment(AppointmentStatus.Requested), ProcedureTitle, AppointmentStatus.Requested, Colleague, Now)
            .Should().BeEmpty();
    }

    [Test]
    public void AfterCancelling_BookedWithNoTime_TellsNobody()
    {
        var appointment = BookedAt(At(NextMonday, 10));
        appointment.StartUtc = null;

        AppointmentNotifications.AfterCancelling(appointment, ProcedureTitle, AppointmentStatus.Booked, Colleague, Now)
            .Should().BeEmpty();
    }
}
