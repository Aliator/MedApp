using AutoFixture;
using FluentAssertions;
using MedApp.Application.Appointments.Commands.BookAppointment;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Notifications;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.AppointmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Commands.BookAppointment;

[TestFixture]
public sealed class BookAppointmentHandlerTests : AppointmentHandlerTestBase
{
    private const string Requested = nameof(AppointmentStatus.Requested);

    private BookAppointmentHandler _handler = null!;

    [SetUp]
    public void SetUp() => _handler = Fixture.Create<BookAppointmentHandler>();

    private static BookAppointmentCommand Command(
        Appointment appointment,
        Guid? staffUserId = null,
        DateTime? startUtc = null,
        int minutes = 30,
        PathwayActor? by = null,
        AppointmentStatus? expected = null)
    {
        var start = startUtc ?? At(NextMonday, 14);
        // By default the caller saw the appointment as it is: booking a Requested one, moving a Booked one.
        return new BookAppointmentCommand(
            appointment.Id,
            staffUserId ?? StaffUserId,
            start,
            start.AddMinutes(minutes),
            (expected ?? appointment.Status).ToString(),
            by ?? Colleague);
    }

    [Test]
    public async Task Handle_StaffMemberDoesNotExist_ThrowsNotFoundWithoutTakingTheLock()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Requested));

        var act = () => _handler.Handle(Command(appointment, staffUserId: Colleague.UserId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        Lock.Locked.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_AppointmentDoesNotExist_ThrowsNotFound()
    {
        var act = () => _handler.Handle(
            new BookAppointmentCommand(Guid.NewGuid(), StaffUserId, At(NextMonday, 14), At(NextMonday, 15), Requested, Colleague),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        SaveTokens.Should().BeEmpty();
    }

    [TestCase(AppointmentStatus.Attended, "attended")]
    [TestCase(AppointmentStatus.DidNotAttend, "did not attend")]
    [TestCase(AppointmentStatus.Cancelled, "cancelled")]
    public async Task Handle_NeitherRequestedNorBooked_ThrowsConflict(AppointmentStatus status, string described)
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(status));

        var act = () => _handler.Handle(Command(appointment), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"An appointment that's {described} can't be booked or moved.");
        SaveTokens.Should().BeEmpty();
        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ToBookButSomeoneElseBookedItFirst_ThrowsConflictWithoutMovingTheirBooking()
    {
        // Ana booked it at 10:00 with the member of staff; Ben's list still showed it as needing booking.
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10)));

        var act = () => _handler.Handle(
            Command(appointment, startUtc: At(NextMonday, 15), expected: AppointmentStatus.Requested),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(AppointmentWorkflow.BookedMeanwhileMessage);
        appointment.StartUtc.Should().Be(At(NextMonday, 10));
        appointment.StaffUserId.Should().Be(StaffUserId);
        SaveTokens.Should().BeEmpty();
        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ToBookButSomeoneElseBookedItWithAnotherStaffMember_ThrowsConflict()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10)));

        var act = () => _handler.Handle(
            Command(appointment, staffUserId: OtherStaffUserId, expected: AppointmentStatus.Requested),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(AppointmentWorkflow.BookedMeanwhileMessage);
        appointment.StaffUserId.Should().Be(StaffUserId);
        SaveTokens.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ToMoveButItIsNotBooked_ThrowsConflict()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Requested));

        var act = () => _handler.Handle(Command(appointment, expected: AppointmentStatus.Booked), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(AppointmentWorkflow.ChangedMeanwhileMessage);
        appointment.Status.Should().Be(AppointmentStatus.Requested);
        SaveTokens.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ExpectedStatusInAnyCase_Books()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Requested));

        await _handler.Handle(Command(appointment) with { ExpectedStatus = "requested" }, CancellationToken.None);

        appointment.Status.Should().Be(AppointmentStatus.Booked);
    }

    [Test]
    public async Task Handle_SomeoneElseSavedItMeanwhile_PassesTheConflictOnAndNotifiesNobody()
    {
        // The row version: another request saved the appointment after this one loaded it.
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Requested));
        Repository
            .Setup(r => r.SaveAsync(It.IsAny<IEnumerable<AppointmentEvent>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException(AppointmentWorkflow.ChangedMeanwhileMessage));

        var act = () => _handler.Handle(Command(appointment), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(AppointmentWorkflow.ChangedMeanwhileMessage);
        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_Requested_BooksItHoldingTheStaffMembersLock()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Requested));
        using var cts = new CancellationTokenSource();

        var response = await _handler.Handle(Command(appointment), cts.Token);

        Lock.LockedOnce.Should().Equal(StaffUserId);
        Lock.Tokens.Should().Equal(cts.Token);
        Repository.Verify(r => r.GetForUpdateAsync(appointment.Id, cts.Token), Times.Once);
        Calendar.Verify(c => c.GetCalendarDataAsync(StaffUserId, NextMonday, NextMonday, cts.Token), Times.Once);
        Identity.Verify(i => i.GetStaffAsync(cts.Token), Times.Once);

        appointment.Status.Should().Be(AppointmentStatus.Booked);
        appointment.StaffUserId.Should().Be(StaffUserId);
        appointment.StartUtc.Should().Be(At(NextMonday, 14));
        appointment.EndUtc.Should().Be(At(NextMonday, 14, 30));
        appointment.BookedByUserId.Should().Be(Colleague.UserId);
        appointment.BookedAtUtc.Should().Be(Now);
        SavedEvents.Should().ContainSingle().Which.Action.Should().Be(AppointmentEventAction.Booked);
        SaveTokens.Should().Equal(cts.Token);

        response.Status.Should().Be(nameof(AppointmentStatus.Booked));
        response.CanReschedule.Should().BeTrue();
        response.CanBook.Should().BeFalse();
    }

    [Test]
    public async Task Handle_RequestedBookedBySomeoneElse_NotifiesTheStaffMember()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Requested));
        using var cts = new CancellationTokenSource();

        await _handler.Handle(Command(appointment), cts.Token);

        var (notification, token) = Sent.Should().ContainSingle().Subject;
        token.Should().Be(cts.Token);
        notification.Kind.Should().Be(NotificationKind.AppointmentBooked);
        notification.UserId.Should().Be(StaffUserId);
        notification.Title.Should().Be("Appointment booked");
        notification.Message.Should().Be($"{Colleague.Username} booked you an appointment on Monday 23 November at 14:00.");
    }

    [Test]
    public async Task Handle_RequestedBookedByTheStaffMemberThemselves_SendsNoNotification()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Requested));

        await _handler.Handle(Command(appointment, by: Self), CancellationToken.None);

        SavedEvents.Should().ContainSingle();
        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ForAPathway_NotificationLinksToItWithAFixedTitle()
    {
        var appointment = CalendarTestConstants.CreateAppointment(AppointmentStatus.Requested);
        var pathway = LinkToPathway(appointment);
        GivenAppointment(appointment);

        await _handler.Handle(Command(appointment), CancellationToken.None);

        var notification = SentNotifications.Single();
        notification.Title.Should().Be("Appointment booked");
        notification.PathwayId.Should().Be(pathway.Id);
        notification.AppointmentId.Should().Be(appointment.Id);
    }

    [Test]
    public async Task Handle_BookedMovedToAnotherTime_ReschedulesAndTellsTheStaffMember()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10)));

        await _handler.Handle(Command(appointment, startUtc: At(NextMonday, 15)), CancellationToken.None);

        appointment.StartUtc.Should().Be(At(NextMonday, 15));
        var entry = SavedEvents.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AppointmentEventAction.Rescheduled);
        entry.Details.Should().Contain(StaffUsername, "the history records who it was with before");

        var notification = SentNotifications.Should().ContainSingle().Subject;
        notification.Kind.Should().Be(NotificationKind.AppointmentRescheduled);
        notification.UserId.Should().Be(StaffUserId);
        notification.Message.Should().Be($"{Colleague.Username} moved your appointment to Monday 23 November at 15:00.");
    }

    [Test]
    public async Task Handle_BookedMovedByTheStaffMemberThemselves_SendsNoNotification()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10)));

        await _handler.Handle(Command(appointment, startUtc: At(NextMonday, 15), by: Self), CancellationToken.None);

        SavedEvents.Should().ContainSingle().Which.Action.Should().Be(AppointmentEventAction.Rescheduled);
        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_BookedMovedToSomeoneElse_LocksTheNewCalendarAndTellsBothOfThem()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10)));

        await _handler.Handle(Command(appointment, staffUserId: OtherStaffUserId, startUtc: At(NextMonday, 10)), CancellationToken.None);

        // Moving it off someone's calendar can't make a clash there, so only the new calendar is locked and read.
        Lock.LockedOnce.Should().Equal(OtherStaffUserId);
        Calendar.Verify(c => c.GetCalendarDataAsync(OtherStaffUserId, NextMonday, NextMonday, It.IsAny<CancellationToken>()), Times.Once);

        appointment.StaffUserId.Should().Be(OtherStaffUserId);
        var entry = SavedEvents.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AppointmentEventAction.Rescheduled);
        entry.StaffUsername.Should().Be(OtherStaffUsername);

        SentNotifications.Should().HaveCount(2);
        SentNotifications.Should().ContainSingle(n => n.UserId == OtherStaffUserId)
            .Which.Kind.Should().Be(NotificationKind.AppointmentBooked);
        SentNotifications.Should().ContainSingle(n => n.UserId == StaffUserId)
            .Which.Kind.Should().Be(NotificationKind.AppointmentCancelled);
    }

    [Test]
    public async Task Handle_BookedToWhereItAlreadyIs_SavesAndSendsNothing()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 14)));

        var response = await _handler.Handle(Command(appointment), CancellationToken.None);

        SaveTokens.Should().BeEmpty();
        Sent.Should().BeEmpty();
        response.StartUtc.Should().Be(At(NextMonday, 14));
    }

    [Test]
    public async Task Handle_MovedOverlappingWhereItWas_IgnoresItself()
    {
        var appointment = CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 14));
        GivenAppointment(appointment);
        CalendarData = CreateData(NextMonday, appointments: [appointment]);

        await _handler.Handle(Command(appointment, startUtc: At(NextMonday, 14, 15)), CancellationToken.None);

        appointment.StartUtc.Should().Be(At(NextMonday, 14, 15));
        SaveTokens.Should().ContainSingle();
    }

    [Test]
    public async Task Handle_Clash_ThrowsConflictNamingItAndChangesNothing()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Requested));
        CalendarData = CreateData(NextMonday, templateBlocks: [CreateTemplateBlock(14, 15)]);

        var act = () => _handler.Handle(Command(appointment), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("That time overlaps Lunch, 14:00 to 15:00.");
        appointment.Status.Should().Be(AppointmentStatus.Requested);
        SaveTokens.Should().BeEmpty();
        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ClashWithAnotherAppointment_ThrowsConflict()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Requested));
        CalendarData = CreateData(NextMonday, appointments: [CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 14, 15))]);

        var act = () => _handler.Handle(Command(appointment), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        SaveTokens.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_TimeWithoutAKind_IsTakenAsUtc()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Requested));
        var start = DateTime.SpecifyKind(At(NextMonday, 14), DateTimeKind.Unspecified);

        await _handler.Handle(
            new BookAppointmentCommand(appointment.Id, StaffUserId, start, start.AddMinutes(30), Requested, Colleague),
            CancellationToken.None);

        appointment.StartUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }
}
