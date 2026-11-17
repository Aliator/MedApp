using AutoFixture;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Calendar;
using MedApp.UnitTests.Common.Constants;
using Moq;

namespace MedApp.UnitTests.Common.Fixtures;

/// <summary>
/// Shared setup for the calendar setup handlers' tests (activity types, day and week templates): a mocked
/// <see cref="ICalendarSetupRepository"/> where nothing is in use, no names are taken, and the activity types and day
/// templates the test registers are the ones that exist (whoever owns them); an identity service listing
/// <see cref="CalendarTestConstants.Staff"/>; and a clock fixed at <see cref="CalendarTestConstants.Now"/>.
/// </summary>
public abstract class CalendarSetupHandlerTestBase
{
    private readonly List<CalendarActivityType> _activityTypes = [];
    private readonly List<DayTemplate> _dayTemplates = [];

    protected IFixture Fixture { get; private set; } = null!;
    protected Mock<ICalendarSetupRepository> Repository { get; private set; } = null!;
    protected Mock<IIdentityReadService> Identity { get; private set; } = null!;
    protected CancellationTokenSource Cts { get; private set; } = null!;
    protected CancellationToken Token => Cts.Token;

    [SetUp]
    public void SetUpCalendarSetupMocks()
    {
        Fixture = AutoFixtureFactory.Create();
        Repository = new Mock<ICalendarSetupRepository>();
        Identity = new Mock<IIdentityReadService>();
        Cts = new CancellationTokenSource();
        _activityTypes.Clear();
        _dayTemplates.Clear();

        Repository
            .Setup(r => r.GetActivityTypesInUseAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>());
        Repository
            .Setup(r => r.GetDayTemplateUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DayTemplateUsage>());
        Repository
            .Setup(r => r.GetWeekTemplateUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, WeekTemplateUsage>());
        Repository
            .Setup(r => r.GetActivityTypesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                (IReadOnlyList<CalendarActivityType>)_activityTypes.Where(t => ids.Contains(t.Id)).ToList());
        Repository
            .Setup(r => r.GetDayTemplatesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                (IReadOnlyList<DayTemplate>)_dayTemplates.Where(t => ids.Contains(t.Id)).ToList());

        Identity
            .Setup(i => i.GetStaffAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CalendarTestConstants.Staff);

        Fixture.Inject(Repository.Object);
        Fixture.Inject(Identity.Object);
        Fixture.Inject<TimeProvider>(new FixedTimeProvider(CalendarTestConstants.Now));
    }

    [TearDown]
    public void TearDownCalendarSetupMocks() => Cts.Dispose();

    /// <summary>Makes these activity types exist (for lookups by ID and for <c>GetActivityTypeAsync</c>).</summary>
    protected void GivenActivityTypes(params CalendarActivityType[] activityTypes)
    {
        foreach (var activityType in activityTypes)
        {
            _activityTypes.Add(activityType);
            Repository.Setup(r => r.GetActivityTypeAsync(activityType.Id, It.IsAny<CancellationToken>())).ReturnsAsync(activityType);
        }
    }

    /// <summary>Makes these day templates exist, for lookups by ID (without their blocks).</summary>
    protected void GivenDayTemplates(params DayTemplate[] dayTemplates) => _dayTemplates.AddRange(dayTemplates);

    /// <summary>Marks these activity types as used by a block or template block.</summary>
    protected void GivenActivityTypesInUse(params Guid[] ids) =>
        Repository
            .Setup(r => r.GetActivityTypesInUseAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> asked, CancellationToken _) =>
                (IReadOnlySet<Guid>)asked.Where(ids.Contains).ToHashSet());

    protected void GivenDayTemplateUsage(Guid id, DayTemplateUsage usage) =>
        Repository
            .Setup(r => r.GetDayTemplateUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DayTemplateUsage> { [id] = usage });

    protected void GivenWeekTemplateUsage(Guid id, WeekTemplateUsage usage) =>
        Repository
            .Setup(r => r.GetWeekTemplateUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, WeekTemplateUsage> { [id] = usage });
}
