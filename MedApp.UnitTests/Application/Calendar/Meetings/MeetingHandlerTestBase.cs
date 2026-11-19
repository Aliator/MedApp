using AutoFixture;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Domain.Calendar;
using MedApp.Domain.Notifications;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.MeetingsTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Meetings;

/// <summary>
/// Shared setup for the handler tests of meetings, the team view and suggested times: a mocked meeting repository that records what it adds, saves and deletes;
/// calendars that are empty (just the working week) for everyone unless a test gives one (<see cref="GivenCalendar"/>);
/// a calendar lock that runs the work straight away and records it; an identity service listing
/// <see cref="Staff"/>; a notification sender that records what it's sent; and a clock fixed at <see cref="Now"/>.
/// A cancellation token of its own is passed to each handler.
/// </summary>
public abstract class MeetingHandlerTestBase
{
    private readonly Dictionary<Guid, CalendarData> _calendars = [];

    protected IFixture Fixture { get; private set; } = null!;
    protected Mock<IMeetingRepository> Meetings { get; private set; } = null!;
    protected Mock<ICalendarRepository> Calendars { get; private set; } = null!;
    protected Mock<IIdentityReadService> Identity { get; private set; } = null!;
    protected RecordingCalendarLock Lock { get; private set; } = null!;
    protected CancellationTokenSource Cts { get; private set; } = null!;
    protected CancellationToken Token => Cts.Token;

    /// <summary>Notifications sent, and the token each was sent with.</summary>
    protected List<(Notification Notification, CancellationToken Token)> Sent { get; } = [];

    /// <summary>Meetings passed to AddAsync.</summary>
    protected List<Meeting> Added { get; } = [];

    /// <summary>
    /// The teams of members of staff named here (the team walls); anyone else is in
    /// <see cref="MedApp.UnitTests.Common.Constants.PathwaysTestConstants.TeamId"/>.
    /// </summary>
    protected Dictionary<Guid, IReadOnlySet<Guid>> StaffTeams { get; } = [];

    /// <summary>The teams of a member of staff, as the calendar repository gives them.</summary>
    protected IReadOnlySet<Guid> TeamsOf(Guid staffUserId) =>
        StaffTeams.GetValueOrDefault(staffUserId) ?? new HashSet<Guid> { MedApp.UnitTests.Common.Constants.PathwaysTestConstants.TeamId };

    /// <summary>Each read of several calendars: whose, the dates, and the token.</summary>
    protected List<(List<Guid> StaffUserIds, DateOnly From, DateOnly To, CancellationToken Token)> CalendarReads { get; } = [];

    [SetUp]
    public void SetUpMeetingMocks()
    {
        Fixture = AutoFixtureFactory.Create();
        Meetings = new Mock<IMeetingRepository>();
        Calendars = new Mock<ICalendarRepository>();
        Identity = new Mock<IIdentityReadService>();
        Lock = new RecordingCalendarLock();
        Cts = new CancellationTokenSource();
        Sent.Clear();
        Added.Clear();
        CalendarReads.Clear();
        _calendars.Clear();
        StaffTeams.Clear();

        Identity.Setup(i => i.GetStaffAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Staff);
        Identity
            .Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                (IReadOnlyDictionary<Guid, string>)Usernames.Where(kv => ids.Contains(kv.Key)).ToDictionary());

        Calendars
            .Setup(r => r.GetCalendarsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, DateOnly from, DateOnly to, CancellationToken token) =>
            {
                CalendarReads.Add((ids.ToList(), from, to, token));
                return (IReadOnlyList<CalendarData>)ids
                    .Select(id => _calendars.GetValueOrDefault(id) ?? CalendarOf(id, from, to))
                    .ToList();
            });

        Calendars
            .Setup(r => r.GetTeamsOfStaffAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                (IReadOnlyDictionary<Guid, IReadOnlySet<Guid>>)ids.Distinct().ToDictionary(id => id, TeamsOf));
        Calendars
            .Setup(r => r.GetTeamMemberIdsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid teamId, CancellationToken _) =>
                (IReadOnlyList<Guid>)Staff.Select(s => s.UserId).Where(id => TeamsOf(id).Contains(teamId)).ToList());
        Calendars
            .Setup(r => r.GetStaffWeekTemplatesOnAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        Meetings
            .Setup(r => r.AddAsync(It.IsAny<Meeting>(), It.IsAny<CancellationToken>()))
            .Callback((Meeting meeting, CancellationToken _) => Added.Add(meeting))
            .Returns(Task.CompletedTask);

        var notifications = new Mock<INotificationSender>();
        notifications
            .Setup(n => n.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback((Notification notification, CancellationToken token) => Sent.Add((notification, token)))
            .Returns(Task.CompletedTask);

        Fixture.Inject(Meetings.Object);
        Fixture.Inject(Calendars.Object);
        Fixture.Inject(Identity.Object);
        Fixture.Inject(notifications.Object);
        Fixture.Inject<ICalendarLock>(Lock);
        Fixture.Inject<TimeProvider>(new FixedTimeProvider(Now));
    }

    [TearDown]
    public void TearDownMeetingMocks() => Cts.Dispose();

    /// <summary>Makes this the calendar read for the person it belongs to, whatever dates are asked for.</summary>
    protected CalendarData GivenCalendar(CalendarData data)
    {
        _calendars[data.StaffUserId] = data;
        return data;
    }

    /// <summary>Asserts the lock was taken once, for these members of staff, with the handler's token.</summary>
    protected void VerifyLockedOnce(params Guid[] staffUserIds)
    {
        Assert.That(Lock.LockedOnce, Is.EquivalentTo(staffUserIds));
        Assert.That(Lock.Tokens.Single(), Is.EqualTo(Token));
    }

    protected void VerifyNotLocked() => Assert.That(Lock.Locked, Is.Empty, "The calendar lock should not have been taken.");
}
