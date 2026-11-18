using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.ClosedDays.Queries.GetClosedDays;
using MedApp.Application.Calendar.Repositories;
using MedApp.Domain.Calendar;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.ClosedDays.Queries.GetClosedDays;

[TestFixture]
public sealed class GetClosedDaysHandlerTests
{
    [Test]
    public async Task Handle_ReturnsTheRangesDaysInOrder_PastOnesMarked()
    {
        var fixture = AutoFixtureFactory.Create();
        var repository = new Mock<IClosedDayRepository>();
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 12, 31);
        using var cts = new CancellationTokenSource();
        var past = CreateClosedDay(new DateOnly(2026, 8, 31), "Summer bank holiday");
        var upcoming = CreateClosedDay();
        repository.Setup(r => r.GetAsync(from, to, cts.Token)).ReturnsAsync(new List<ClosedDay> { past, upcoming });
        fixture.Inject(repository.Object);
        fixture.Inject<TimeProvider>(new FixedTimeProvider(Now));

        var response = await fixture.Create<GetClosedDaysHandler>().Handle(new GetClosedDaysQuery(from, to), cts.Token);

        response.Select(d => (d.Id, d.Date, d.Name, d.IsPast)).Should().Equal(
            (past.Id, past.Date, "Summer bank holiday", true),
            (upcoming.Id, ClosedWednesday, ClosedDayName, false));
    }
}
