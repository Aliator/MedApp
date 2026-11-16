using AutoFixture;
using FluentAssertions;
using MedApp.Application.Appointments.Commands.CancelAppointment;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Appointments;
using MedApp.Domain.Notifications;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.AppointmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Commands.CancelAppointment;

[TestFixture]
public sealed class CancelAppointmentHandlerTests : AppointmentHandlerTestBase
{
    private CancelAppointmentHandler _handler = null!;

    [SetUp]
    public void SetUp() => _handler = Fixture.Create<CancelAppointmentHandler>();

    [Test]
    public async Task Handle_AppointmentDoesNotExist_ThrowsNotFound()
    {
        var act = () => _handler.Handle(new CancelAppointmentCommand(Guid.NewGuid(), CancelReason, Colleague), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [TestCase(AppointmentStatus.Attended, "attended")]
    [TestCase(AppointmentStatus.DidNotAttend, "did not attend")]
    [TestCase(AppointmentStatus.Cancelled, "cancelled")]
    public async Task Handle_NotOpen_ThrowsConflict(AppointmentStatus status, string described)
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(status));

        var act = () => _handler.Handle(new CancelAppointmentCommand(appointment.Id, CancelReason, Colleague), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"An appointment that's {described} can't be cancelled.");
        appointment.Status.Should().Be(status);
        SaveTokens.Should().BeEmpty();
        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_BookedWithSomeoneElse_CancelsItWithTheTrimmedReasonAndTellsThem()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10)));
        using var cts = new CancellationTokenSource();

        var response = await _handler.Handle(new CancelAppointmentCommand(appointment.Id, $"  {CancelReason}  ", Colleague), cts.Token);

        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
        appointment.CancelReason.Should().Be(CancelReason);
        var entry = SavedEvents.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AppointmentEventAction.Cancelled);
        entry.StaffUsername.Should().Be(StaffUsername);
        SaveTokens.Should().Equal(cts.Token);
        Repository.Verify(r => r.GetForUpdateAsync(appointment.Id, cts.Token), Times.Once);

        var (notification, token) = Sent.Should().ContainSingle().Subject;
        token.Should().Be(cts.Token);
        notification.Kind.Should().Be(NotificationKind.AppointmentCancelled);
        notification.UserId.Should().Be(StaffUserId);
        notification.Message.Should().Be($"{Colleague.Username} cancelled your appointment on Monday 23 November at 10:00.");
        notification.Message.Should().NotContain(CancelReason);

        response.Status.Should().Be(nameof(AppointmentStatus.Cancelled));
        response.CancelReason.Should().Be(CancelReason);
        response.CanCancel.Should().BeFalse();
        response.CanEditNotes.Should().BeFalse();
    }

    [Test]
    public async Task Handle_OwnAppointment_SendsNoNotification()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked));

        await _handler.Handle(new CancelAppointmentCommand(appointment.Id, CancelReason, Self), CancellationToken.None);

        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_Requested_CancelsItTellingNobody()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Requested));

        await _handler.Handle(new CancelAppointmentCommand(appointment.Id, CancelReason, Colleague), CancellationToken.None);

        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
        SavedEvents.Single().StaffUsername.Should().BeNull();
        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ForAProcedure_NotificationLinksToItWithAFixedTitle()
    {
        var appointment = CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked);
        var procedure = LinkToProcedure(appointment);
        GivenAppointment(appointment);

        await _handler.Handle(new CancelAppointmentCommand(appointment.Id, CancelReason, Colleague), CancellationToken.None);

        var notification = SentNotifications.Single();
        notification.Title.Should().Be("Appointment cancelled");
        notification.ProcedureId.Should().Be(procedure.Id);
    }

    [Test]
    public async Task Handle_PatientRestricted_StillCancelsIt()
    {
        var appointment = CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10));
        appointment.Patient = PrivacyTestConstants.CreateRestrictedPatient();
        GivenAppointment(appointment);

        await _handler.Handle(new CancelAppointmentCommand(appointment.Id, CancelReason, Colleague), CancellationToken.None);

        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
        SavedEvents.Should().ContainSingle().Which.Action.Should().Be(AppointmentEventAction.Cancelled);
    }

    [Test]
    public async Task Handle_PatientAnonymised_ThrowsConflictWithoutCancelling()
    {
        var appointment = CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10));
        appointment.Patient = PrivacyTestConstants.CreateAnonymisedPatient();
        GivenAppointment(appointment);

        var act = () => _handler.Handle(new CancelAppointmentCommand(appointment.Id, CancelReason, Colleague), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        appointment.Status.Should().Be(AppointmentStatus.Booked);
        SaveTokens.Should().BeEmpty();
        Sent.Should().BeEmpty();
    }
}
