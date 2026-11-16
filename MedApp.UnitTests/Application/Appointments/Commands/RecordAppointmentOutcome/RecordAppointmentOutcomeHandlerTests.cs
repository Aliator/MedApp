using AutoFixture;
using FluentAssertions;
using MedApp.Application.Appointments.Commands.RecordAppointmentOutcome;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Appointments;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.AppointmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Commands.RecordAppointmentOutcome;

[TestFixture]
public sealed class RecordAppointmentOutcomeHandlerTests : AppointmentHandlerTestBase
{
    private RecordAppointmentOutcomeHandler _handler = null!;

    [SetUp]
    public void SetUp() => _handler = Fixture.Create<RecordAppointmentOutcomeHandler>();

    private Appointment GivenStarted(AppointmentStatus status = AppointmentStatus.Booked) =>
        GivenAppointment(CalendarTestConstants.CreateAppointment(status, Now.AddHours(-1)));

    [Test]
    public async Task Handle_AppointmentDoesNotExist_ThrowsNotFound()
    {
        var act = () => _handler.Handle(new RecordAppointmentOutcomeCommand(Guid.NewGuid(), "Attended", Colleague), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_NotStartedYet_ThrowsConflict()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, Now.AddMinutes(5)));

        var act = () => _handler.Handle(new RecordAppointmentOutcomeCommand(appointment.Id, "Attended", Colleague), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("Whether the patient attended can be recorded once the appointment has started.");
        appointment.Status.Should().Be(AppointmentStatus.Booked);
        SaveTokens.Should().BeEmpty();
    }

    [TestCase(AppointmentStatus.Requested)]
    [TestCase(AppointmentStatus.Cancelled)]
    public async Task Handle_NotBooked_ThrowsConflict(AppointmentStatus status)
    {
        var appointment = GivenStarted(status);

        var act = () => _handler.Handle(new RecordAppointmentOutcomeCommand(appointment.Id, "Attended", Colleague), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*has no outcome to record.");
        SaveTokens.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_StartedBooked_MarksItAttendedAndSaves()
    {
        var appointment = GivenStarted();
        using var cts = new CancellationTokenSource();

        var response = await _handler.Handle(new RecordAppointmentOutcomeCommand(appointment.Id, "attended", Colleague), cts.Token);

        appointment.Status.Should().Be(AppointmentStatus.Attended);
        appointment.OutcomeByUserId.Should().Be(Colleague.UserId);
        appointment.OutcomeAtUtc.Should().Be(Now);
        var entry = SavedEvents.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AppointmentEventAction.Attended);
        entry.StaffUsername.Should().Be(StaffUsername);
        SaveTokens.Should().Equal(cts.Token);
        Repository.Verify(r => r.GetForUpdateAsync(appointment.Id, cts.Token), Times.Once);
        Identity.Verify(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), cts.Token), Times.Exactly(2));

        response.Status.Should().Be(nameof(AppointmentStatus.Attended));
        response.OutcomeByUsername.Should().Be(Colleague.Username);
        response.CanRecordOutcome.Should().BeTrue("it can still be corrected");
    }

    [Test]
    public async Task Handle_Attended_CorrectsItToDidNotAttend()
    {
        var appointment = GivenStarted(AppointmentStatus.Attended);

        await _handler.Handle(new RecordAppointmentOutcomeCommand(appointment.Id, "DidNotAttend", Colleague), CancellationToken.None);

        appointment.Status.Should().Be(AppointmentStatus.DidNotAttend);
        SavedEvents.Should().ContainSingle().Which.Action.Should().Be(AppointmentEventAction.DidNotAttend);
    }

    [Test]
    public async Task Handle_SameOutcomeAsBefore_SavesNothing()
    {
        var appointment = GivenStarted(AppointmentStatus.DidNotAttend);

        var response = await _handler.Handle(new RecordAppointmentOutcomeCommand(appointment.Id, "DidNotAttend", Colleague), CancellationToken.None);

        SaveTokens.Should().BeEmpty();
        response.Status.Should().Be(nameof(AppointmentStatus.DidNotAttend));
    }

    [Test]
    public async Task Handle_StaffMemberDeleted_RecordsNoStaffUsername()
    {
        var appointment = CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, Now.AddHours(-1));
        appointment.StaffUserId = Guid.NewGuid();
        GivenAppointment(appointment);

        var response = await _handler.Handle(new RecordAppointmentOutcomeCommand(appointment.Id, "Attended", Colleague), CancellationToken.None);

        SavedEvents.Single().StaffUsername.Should().BeNull();
        response.StaffUsername.Should().Be("Deleted user");
    }
}
