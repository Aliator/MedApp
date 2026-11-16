using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Appointments.Queries.GetAppointmentById;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Activity;
using MedApp.Domain.Appointments;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Queries.GetAppointmentById;

[TestFixture]
public sealed class GetAppointmentByIdHandlerTests : AppointmentHandlerTestBase
{
    private GetAppointmentByIdHandler _handler = null!;

    [SetUp]
    public void SetUp() => _handler = Fixture.Create<GetAppointmentByIdHandler>();

    [Test]
    public async Task Handle_AppointmentDoesNotExist_ThrowsNotFound()
    {
        var act = () => _handler.Handle(new GetAppointmentByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_Exists_ReturnsItWithItsUsernamesWorkedOutNow()
    {
        var appointment = CreateAppointment(AppointmentStatus.Booked, Now.AddMinutes(-10));
        using var cts = new CancellationTokenSource();
        Repository.Setup(r => r.GetByIdAsync(appointment.Id, cts.Token)).ReturnsAsync(appointment);

        var response = await _handler.Handle(new GetAppointmentByIdQuery(appointment.Id), cts.Token);

        response.Id.Should().Be(appointment.Id);
        response.StaffUsername.Should().Be(StaffUsername);
        response.CanRecordOutcome.Should().BeTrue("it started before the fixed clock's now");
        Identity.Verify(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), cts.Token), Times.Once);
        Repository.Verify(r => r.GetForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_Exists_RecordsAViewOfItsPatient()
    {
        var activity = new RecordingActivityLog();
        Fixture.Inject<IActivityLog>(activity);
        var appointment = CreateAppointment();
        Repository.Setup(r => r.GetByIdAsync(appointment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(appointment);
        using var cts = new CancellationTokenSource();

        await Fixture.Create<GetAppointmentByIdHandler>().Handle(new GetAppointmentByIdQuery(appointment.Id), cts.Token);

        activity.Views.Should().ContainSingle().Which.Should().BeEquivalentTo(
            (ActivityAction.PatientViewed, new List<Guid> { appointment.PatientId }, (Guid?)null));
        activity.Tokens.Should().Equal(cts.Token);
    }

    [Test]
    public async Task Handle_AppointmentDoesNotExist_RecordsNothing()
    {
        var activity = new RecordingActivityLog();
        Fixture.Inject<IActivityLog>(activity);

        var act = () => Fixture.Create<GetAppointmentByIdHandler>().Handle(new GetAppointmentByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        activity.Views.Should().BeEmpty();
    }
}
