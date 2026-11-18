using AutoFixture;
using MedApp.Application.Activity.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Clinics.Common;
using MedApp.Application.Clinics.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Specialties.Repositories;
using MedApp.Domain.Appointments;
using MedApp.Domain.Clinics;
using MedApp.Domain.Notifications;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.ClinicsTestConstants;

namespace MedApp.UnitTests.Application.Clinics;

/// <summary>
/// Shared setup for clinic handler tests: a mocked clinic repository whose rooms are free (<see cref="RoomDays"/>, keyed
/// by room) and which records what it adds and saves; empty calendars (the default working week) unless a test gives one
/// (<see cref="Calendar"/>); the synthetic specialty; the calendar staff; a calendar lock that runs the work at once and
/// records it; recorded notifications and activity; and a clock fixed at <see cref="CalendarTestConstants.Now"/>.
/// </summary>
public abstract class ClinicHandlerTestBase
{
    protected IFixture Fixture { get; private set; } = null!;
    protected Mock<IClinicRepository> Clinics { get; private set; } = null!;
    protected Mock<ICalendarRepository> Calendars { get; private set; } = null!;
    protected RecordingCalendarLock Lock { get; private set; } = null!;
    protected RecordingActivityLog Activity { get; private set; } = null!;
    protected CancellationTokenSource Cts { get; private set; } = null!;
    protected CancellationToken Token => Cts.Token;

    /// <summary>What each room has on the day asked for: free, by default.</summary>
    protected Dictionary<Guid, RoomDay> RoomDays { get; } = [];

    /// <summary>The calendar read for <see cref="CalendarTestConstants.StaffUserId"/>, whatever the dates: empty by default.</summary>
    protected CalendarData? Calendar { get; set; }

    protected List<ClinicSession> AddedSessions { get; } = [];
    protected List<IReadOnlyList<AppointmentEvent>> Saved { get; } = [];
    protected List<Notification> Sent { get; } = [];

    [SetUp]
    public void SetUpClinicMocks()
    {
        Fixture = AutoFixtureFactory.Create();
        Clinics = new Mock<IClinicRepository>();
        Calendars = new Mock<ICalendarRepository>();
        Lock = new RecordingCalendarLock();
        Activity = new RecordingActivityLog();
        Cts = new CancellationTokenSource();
        RoomDays.Clear();
        AddedSessions.Clear();
        Saved.Clear();
        Sent.Clear();
        Calendar = null;

        Clinics
            .Setup(r => r.GetRoomDayAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid roomId, DateOnly date, CancellationToken _) =>
                RoomDays.GetValueOrDefault(roomId) ?? CreateRoomDay(date, Room(roomId)));
        Clinics
            .Setup(r => r.AddSessionAsync(It.IsAny<ClinicSession>(), It.IsAny<CancellationToken>()))
            .Callback((ClinicSession session, CancellationToken _) => AddedSessions.Add(session))
            .Returns(Task.CompletedTask);
        Clinics
            .Setup(r => r.SaveAsync(It.IsAny<IEnumerable<AppointmentEvent>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<AppointmentEvent> events, string _, CancellationToken _) => Saved.Add(events.ToList()))
            .Returns(Task.CompletedTask);
        Clinics
            .Setup(r => r.GetBookedStartsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<DateTime>>());
        Clinics
            .Setup(r => r.GetSessionAppointmentsForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        Clinics
            .Setup(r => r.GetSessionAppointmentsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        Calendars
            .Setup(r => r.GetCalendarDataAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, DateOnly from, DateOnly to, CancellationToken _) => Calendar ?? CreateData(from, to));

        var identity = new Mock<IIdentityReadService>();
        identity.Setup(i => i.GetStaffAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Staff);
        identity
            .Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                (IReadOnlyDictionary<Guid, string>)Staff.Where(s => ids.Contains(s.UserId)).ToDictionary(s => s.UserId, s => s.Username));

        var notifications = new Mock<INotificationSender>();
        notifications
            .Setup(n => n.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback((Notification notification, CancellationToken _) => Sent.Add(notification))
            .Returns(Task.CompletedTask);

        Fixture.Inject(Clinics.Object);
        Fixture.Inject(Calendars.Object);
        Fixture.Inject(identity.Object);
        Fixture.Inject(notifications.Object);
        Fixture.Inject<ISpecialtyRepository>(SpecialtiesTestConstants.RepositoryWith(Specialty()).Object);
        Fixture.Inject<ICalendarLock>(Lock);
        Fixture.Inject<IActivityLog>(Activity);
        Fixture.Inject<TimeProvider>(new FixedTimeProvider(Now));
    }

    [TearDown]
    public void TearDownClinicMocks() => Cts.Dispose();

    /// <summary>The session the repository finds, read-only and for update (the same copy).</summary>
    protected ClinicSession GivenSession(ClinicSession session)
    {
        Clinics.Setup(r => r.GetSessionAsync(session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        Clinics.Setup(r => r.GetSessionForUpdateAsync(session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        return session;
    }
}
