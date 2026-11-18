using AutoFixture;
using FluentAssertions;
using MedApp.Application.Appointments.Commands.BookAppointment;
using MedApp.Application.Appointments.Commands.CancelAppointment;
using MedApp.Application.PatientMessages.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.PatientMessages;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.AppointmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Commands;

/// <summary>
/// Telling the patient of a move or a cancellation (centre plan decision 130): staged on the outbox before the
/// change is saved, so both are saved together, and only when asked, for a booking that's moved or still to come.
/// </summary>
[TestFixture]
public sealed class TellPatientTests : AppointmentHandlerTestBase
{
    private Mock<IPatientMessageOutbox> _outbox = null!;
    private List<string> _order = null!;

    [SetUp]
    public void SetUp()
    {
        _order = [];
        _outbox = new Mock<IPatientMessageOutbox>();
        _outbox.Setup(o => o.StageAsync(It.IsAny<Appointment>(), It.IsAny<PatientMessageKind>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback(() => _order.Add("stage"))
            .Returns(Task.CompletedTask);
        Repository.Setup(r => r.SaveAsync(It.IsAny<IEnumerable<AppointmentEvent>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<AppointmentEvent> events, CancellationToken token) =>
            {
                SavedEvents.AddRange(events);
                SaveTokens.Add(token);
                _order.Add("save");
            })
            .Returns(Task.CompletedTask);
        Fixture.Inject(_outbox.Object);
    }

    private BookAppointmentCommand Move(Appointment appointment, DateTime startUtc, bool tell) =>
        new(appointment.Id, StaffUserId, startUtc, startUtc.AddMinutes(30), nameof(AppointmentStatus.Booked), Colleague, TellPatient: tell);

    [Test]
    public async Task Book_MovedToAnotherTimeAndAsked_StagesAMoveMessageBeforeSaving()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10)));
        using var cts = new CancellationTokenSource();

        await Fixture.Create<BookAppointmentHandler>().Handle(Move(appointment, At(NextMonday, 14), tell: true), cts.Token);

        _outbox.Verify(o => o.StageAsync(appointment, PatientMessageKind.AppointmentMoved, Colleague.UserId, Now, It.IsAny<CancellationToken>()), Times.Once);
        _order.Should().Equal("stage", "save");
    }

    [Test]
    public async Task Book_MovedButNotAsked_StagesNothing()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10)));

        await Fixture.Create<BookAppointmentHandler>().Handle(Move(appointment, At(NextMonday, 14), tell: false), CancellationToken.None);

        _outbox.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Book_FirstBookingEvenIfAsked_StagesNothing()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Requested));
        var command = new BookAppointmentCommand(
            appointment.Id, StaffUserId, At(NextMonday, 14), At(NextMonday, 14, 30), nameof(AppointmentStatus.Requested), Colleague, TellPatient: true);

        await Fixture.Create<BookAppointmentHandler>().Handle(command, CancellationToken.None);

        _outbox.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Book_OnlyTheStaffMemberChanged_StagesNothing()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10)));
        var command = new BookAppointmentCommand(
            appointment.Id, OtherStaffUserId, At(NextMonday, 10), At(NextMonday, 10, 30), nameof(AppointmentStatus.Booked), Colleague, TellPatient: true);

        await Fixture.Create<BookAppointmentHandler>().Handle(command, CancellationToken.None);

        _outbox.VerifyNoOtherCalls();
        SavedEvents.Should().ContainSingle(e => e.Action == AppointmentEventAction.Rescheduled);
    }

    [Test]
    public async Task Cancel_BookedStillToComeAndAsked_StagesACancellationBeforeSaving()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10)));

        await Fixture.Create<CancelAppointmentHandler>()
            .Handle(new CancelAppointmentCommand(appointment.Id, CancelReason, Colleague, TellPatient: true), CancellationToken.None);

        _outbox.Verify(o => o.StageAsync(appointment, PatientMessageKind.AppointmentCancelled, Colleague.UserId, Now, It.IsAny<CancellationToken>()), Times.Once);
        _order.Should().Equal("stage", "save");
    }

    [Test]
    public async Task Cancel_RequestedEvenIfAsked_StagesNothing()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Requested));

        await Fixture.Create<CancelAppointmentHandler>()
            .Handle(new CancelAppointmentCommand(appointment.Id, CancelReason, Colleague, TellPatient: true), CancellationToken.None);

        _outbox.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Cancel_BookedButNotAsked_StagesNothing()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10)));

        await Fixture.Create<CancelAppointmentHandler>()
            .Handle(new CancelAppointmentCommand(appointment.Id, CancelReason, Colleague), CancellationToken.None);

        _outbox.VerifyNoOtherCalls();
    }
}
