using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.PatientMessages.Common;
using MedApp.Application.PatientMessages.Queries.GetAppointmentMessages;
using MedApp.Application.PatientMessages.Queries.GetPatientMessagingStatus;
using MedApp.Application.PatientMessages.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Appointments;
using MedApp.Domain.PatientMessages;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.Extensions.Options;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.PatientMessagesTestConstants;

namespace MedApp.UnitTests.Application.PatientMessages.Queries;

[TestFixture]
public sealed class PatientMessageQueriesTests
{
    private IFixture _fixture = null!;
    private Mock<IAppointmentRepository> _appointments = null!;
    private Mock<IPatientMessageRepository> _messages = null!;
    private RecordingActivityLog _activity = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _appointments = new Mock<IAppointmentRepository>();
        _messages = new Mock<IPatientMessageRepository>();
        _activity = new RecordingActivityLog();
        var identity = new Mock<IIdentityReadService>();
        identity.Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
        _fixture.Inject(_appointments.Object);
        _fixture.Inject(_messages.Object);
        _fixture.Inject(identity.Object);
        _fixture.Inject<IActivityLog>(_activity);
    }

    [Test]
    public async Task GetAppointmentMessages_UnknownAppointment_ThrowsNotFoundAndRecordsNothing()
    {
        var act = () => _fixture.Create<GetAppointmentMessagesHandler>().Handle(new GetAppointmentMessagesQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _activity.Views.Should().BeEmpty();
    }

    [Test]
    public async Task GetAppointmentMessages_ListsThemAndRecordsAViewOfThePatient()
    {
        var appointment = CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10));
        _appointments.Setup(r => r.GetByIdAsync(appointment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(appointment);
        var message = Message(appointment, status: PatientMessageStatus.Delivered);
        using var cts = new CancellationTokenSource();
        _messages.Setup(r => r.GetForAppointmentAsync(appointment.Id, cts.Token)).ReturnsAsync([message]);

        var response = await _fixture.Create<GetAppointmentMessagesHandler>().Handle(new GetAppointmentMessagesQuery(appointment.Id), cts.Token);

        response.Should().ContainSingle().Which.Id.Should().Be(message.Id);
        var view = _activity.Views.Should().ContainSingle().Subject;
        view.Action.Should().Be(ActivityAction.PatientViewed);
        view.PatientIds.Should().Equal(appointment.PatientId);
    }

    [TestCase(PatientMessageMode.Off, false, false)]
    [TestCase(PatientMessageMode.Offline, true, true)]
    [TestCase(PatientMessageMode.Live, true, true)]
    public async Task GetPatientMessagingStatus_SaysWhetherAndHow(PatientMessageMode mode, bool isOn, bool missed)
    {
        var handler = new GetPatientMessagingStatusHandler(
            Microsoft.Extensions.Options.Options.Create(Options(mode)),
            Microsoft.Extensions.Options.Options.Create(new ReminderOptions { DaysBefore = 3, MissedMessage = true }));

        var response = await handler.Handle(new GetPatientMessagingStatusQuery(), CancellationToken.None);

        response.Mode.Should().Be(mode.ToString());
        response.IsOn.Should().Be(isOn);
        response.SendsMissedAppointmentMessages.Should().Be(missed);
        response.ReminderDaysBefore.Should().Be(3);
    }

    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(14, true)]
    [TestCase(15, false)]
    public void ReminderOptions_DaysBefore_IsOneToFourteen(int days, bool valid) =>
        new ReminderOptions { DaysBefore = days }.IsValid.Should().Be(valid);
}
