using AutoFixture;
using FluentAssertions;
using MedApp.Application.Appointments.Commands.UpdateAppointmentNotes;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Appointments;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.AppointmentsTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Commands.UpdateAppointmentNotes;

[TestFixture]
public sealed class UpdateAppointmentNotesHandlerTests : AppointmentHandlerTestBase
{
    private UpdateAppointmentNotesHandler _handler = null!;

    [SetUp]
    public void SetUp() => _handler = Fixture.Create<UpdateAppointmentNotesHandler>();

    [Test]
    public async Task Handle_AppointmentDoesNotExist_ThrowsNotFound()
    {
        var act = () => _handler.Handle(new UpdateAppointmentNotesCommand(Guid.NewGuid(), Notes, Colleague), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_Cancelled_ThrowsConflict()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Cancelled));

        var act = () => _handler.Handle(new UpdateAppointmentNotesCommand(appointment.Id, Notes, Colleague), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("A cancelled appointment's notes can't be changed.");
        appointment.Notes.Should().BeEmpty();
        SaveTokens.Should().BeEmpty();
    }

    [TestCase(AppointmentStatus.Requested)]
    [TestCase(AppointmentStatus.Booked)]
    [TestCase(AppointmentStatus.Attended)]
    [TestCase(AppointmentStatus.DidNotAttend)]
    public async Task Handle_Changed_SavesTheNewNotesAndTheirHistory(AppointmentStatus status)
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(status));
        appointment.Notes = OtherNotes;
        using var cts = new CancellationTokenSource();

        var response = await _handler.Handle(new UpdateAppointmentNotesCommand(appointment.Id, Notes, Colleague), cts.Token);

        appointment.Notes.Should().Be(Notes);
        var entry = SavedEvents.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AppointmentEventAction.NotesUpdated);
        entry.Details.Should().Contain(OtherNotes).And.Contain(Notes);
        SaveTokens.Should().Equal(cts.Token);
        Repository.Verify(r => r.GetForUpdateAsync(appointment.Id, cts.Token), Times.Once);
        response.Notes.Should().Be(Notes);
    }

    [Test]
    public async Task Handle_Unchanged_SavesNothing()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment());
        appointment.Notes = Notes;

        var response = await _handler.Handle(new UpdateAppointmentNotesCommand(appointment.Id, Notes, Colleague), CancellationToken.None);

        SaveTokens.Should().BeEmpty();
        response.Notes.Should().Be(Notes);
    }

    [Test]
    public async Task Handle_NotesChanged_SendsNoNotification()
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment());

        await _handler.Handle(new UpdateAppointmentNotesCommand(appointment.Id, Notes, Colleague), CancellationToken.None);

        Sent.Should().BeEmpty();
    }
}
