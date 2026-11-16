using AutoFixture;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Notifications;
using MedApp.UnitTests.Common.Constants;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Common.Fixtures;

/// <summary>
/// Shared setup for appointment handler tests: a mocked appointment repository that records the history entries it's
/// given (and the tokens), an empty calendar for every member of staff, a calendar lock that runs the work straight
/// away and records it, a username and staff lookup backed by <see cref="AppointmentsTestConstants"/>, a notification
/// sender that records what it's sent, and a clock fixed at <see cref="CalendarTestConstants.Now"/>.
/// </summary>
public abstract class AppointmentHandlerTestBase
{
    protected IFixture Fixture { get; private set; } = null!;
    protected Mock<IAppointmentRepository> Repository { get; private set; } = null!;
    protected Mock<ICalendarRepository> Calendar { get; private set; } = null!;
    protected Mock<IIdentityReadService> Identity { get; private set; } = null!;
    protected Mock<INotificationSender> Notifications { get; private set; } = null!;
    protected RecordingCalendarLock Lock { get; private set; } = null!;

    /// <summary>History entries passed to AddAsync or SaveAsync.</summary>
    protected List<AppointmentEvent> SavedEvents { get; } = [];

    /// <summary>Appointments passed to AddAsync.</summary>
    protected List<Appointment> Added { get; } = [];

    /// <summary>The token each AddAsync or SaveAsync was given.</summary>
    protected List<CancellationToken> SaveTokens { get; } = [];

    /// <summary>Notifications sent, and the token each was sent with.</summary>
    protected List<(Notification Notification, CancellationToken Token)> Sent { get; } = [];

    /// <summary>The calendar each member of staff's data is read from; empty (just the working week) by default.</summary>
    protected CalendarData CalendarData { get; set; } = null!;

    [SetUp]
    public void SetUpAppointmentMocks()
    {
        Fixture = AutoFixtureFactory.Create();
        Repository = new Mock<IAppointmentRepository>();
        Calendar = new Mock<ICalendarRepository>();
        Identity = new Mock<IIdentityReadService>();
        Notifications = new Mock<INotificationSender>();
        Lock = new RecordingCalendarLock();
        SavedEvents.Clear();
        Added.Clear();
        SaveTokens.Clear();
        Sent.Clear();
        CalendarData = CreateData(NextMonday);

        Identity
            .Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                (IReadOnlyDictionary<Guid, string>)AppointmentsTestConstants.Usernames
                    .Where(kv => ids.Contains(kv.Key))
                    .ToDictionary());
        Identity
            .Setup(i => i.GetStaffAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(AppointmentsTestConstants.Staff);

        Repository
            .Setup(r => r.AddAsync(It.IsAny<Appointment>(), It.IsAny<IEnumerable<AppointmentEvent>>(), It.IsAny<CancellationToken>()))
            .Callback((Appointment appointment, IEnumerable<AppointmentEvent> events, CancellationToken token) =>
            {
                Added.Add(appointment);
                SavedEvents.AddRange(events);
                SaveTokens.Add(token);
            })
            .Returns(Task.CompletedTask);
        Repository
            .Setup(r => r.SaveAsync(It.IsAny<IEnumerable<AppointmentEvent>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<AppointmentEvent> events, CancellationToken token) =>
            {
                SavedEvents.AddRange(events);
                SaveTokens.Add(token);
            })
            .Returns(Task.CompletedTask);

        Calendar
            .Setup(c => c.GetCalendarDataAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CalendarData);

        Notifications
            .Setup(n => n.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback((Notification notification, CancellationToken token) => Sent.Add((notification, token)))
            .Returns(Task.CompletedTask);

        Fixture.Inject(Repository.Object);
        Fixture.Inject(Calendar.Object);
        Fixture.Inject(Identity.Object);
        Fixture.Inject(Notifications.Object);
        Fixture.Inject<ICalendarLock>(Lock);
        Fixture.Inject<TimeProvider>(new FixedTimeProvider(Now));
    }

    /// <summary>Makes the repository return this appointment, for reading and for updating.</summary>
    protected Appointment GivenAppointment(Appointment appointment)
    {
        Repository.Setup(r => r.GetForUpdateAsync(appointment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(appointment);
        Repository.Setup(r => r.GetByIdAsync(appointment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(appointment);
        return appointment;
    }

    /// <summary>The notifications sent.</summary>
    protected List<Notification> SentNotifications => Sent.Select(s => s.Notification).ToList();
}
