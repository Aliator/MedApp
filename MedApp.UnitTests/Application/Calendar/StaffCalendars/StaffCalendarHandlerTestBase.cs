using AutoFixture;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Calendar;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars;

/// <summary>
/// Shared setup for staff calendar handler tests: mocked calendar and setup repositories, a lock that runs the work
/// straight away and records it, an identity service listing <see cref="Staff"/>, and a clock fixed at
/// <see cref="Now"/> (Monday 16 November 2026, 09:00 UK time). A cancellation token of its own is passed to each handler.
/// </summary>
public abstract class StaffCalendarHandlerTestBase
{
    protected IFixture Fixture { get; private set; } = null!;
    protected Mock<ICalendarRepository> Repository { get; private set; } = null!;
    protected Mock<ICalendarSetupRepository> SetupRepository { get; private set; } = null!;
    protected Mock<IIdentityReadService> Identity { get; private set; } = null!;
    protected RecordingCalendarLock Lock { get; private set; } = null!;
    protected CancellationTokenSource Cts { get; private set; } = null!;
    protected CancellationToken Token => Cts.Token;

    [SetUp]
    public void SetUpCalendarMocks()
    {
        Fixture = AutoFixtureFactory.Create();
        Repository = new Mock<ICalendarRepository>();
        SetupRepository = new Mock<ICalendarSetupRepository>();
        Identity = new Mock<IIdentityReadService>();
        Lock = new RecordingCalendarLock();
        Cts = new CancellationTokenSource();

        Identity.Setup(i => i.GetStaffAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Staff);

        Fixture.Inject(Repository.Object);
        Fixture.Inject(SetupRepository.Object);
        Fixture.Inject(Identity.Object);
        Fixture.Inject<ICalendarLock>(Lock);
        Fixture.Inject<TimeProvider>(new FixedTimeProvider(Now));
    }

    [TearDown]
    public void TearDownCalendarMocks() => Cts.Dispose();

    /// <summary>Makes the repository return this calendar data for the person and range it covers.</summary>
    protected CalendarData GivenCalendar(CalendarData data)
    {
        Repository
            .Setup(r => r.GetCalendarDataAsync(data.StaffUserId, data.From, data.To, It.IsAny<CancellationToken>()))
            .ReturnsAsync(data);
        return data;
    }

    /// <summary>Makes the setup repository return this activity type.</summary>
    protected CalendarActivityType GivenActivityType(CalendarActivityType activityType)
    {
        SetupRepository
            .Setup(r => r.GetActivityTypeAsync(activityType.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activityType);
        return activityType;
    }

    /// <summary>Asserts the lock was taken once, for these members of staff, with the handler's token.</summary>
    protected void VerifyLockedOnce(params Guid[] staffUserIds)
    {
        Assert.That(Lock.LockedOnce, Is.EquivalentTo(staffUserIds));
        Assert.That(Lock.Tokens.Single(), Is.EqualTo(Token));
    }

    protected void VerifyNotLocked() => Assert.That(Lock.Locked, Is.Empty, "The calendar lock should not have been taken.");
}
