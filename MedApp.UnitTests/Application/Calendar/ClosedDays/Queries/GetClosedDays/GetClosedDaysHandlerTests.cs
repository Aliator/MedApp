using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.ClosedDays.Queries.GetClosedDays;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Sites.Repositories;
using MedApp.Domain.Calendar;
using MedApp.UnitTests.Common.Constants;
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
        var sites = fixture.Freeze<Mock<ISiteRepository>>();
        fixture.Inject<TimeProvider>(new FixedTimeProvider(Now));

        var response = await fixture.Create<GetClosedDaysHandler>().Handle(new GetClosedDaysQuery(from, to), cts.Token);

        response.Select(d => (d.Id, d.Date, d.Name, d.IsPast)).Should().Equal(
            (past.Id, past.Date, "Summer bank holiday", true),
            (upcoming.Id, ClosedWednesday, ClosedDayName, false));
        response.Should().OnlyContain(d => d.SiteId == null && d.SiteName == null);
        sites.Verify(
            r => r.GetSitesAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never, "no site to name");
    }

    [Test]
    public async Task Handle_SitesDays_NameTheirSites_ArchivedOnesToo()
    {
        var fixture = AutoFixtureFactory.Create();
        var repository = new Mock<IClosedDayRepository>();
        var sites = new Mock<ISiteRepository>();
        using var cts = new CancellationTokenSource();
        var atSite = InMemoryClosedDays.On(ClosedWednesday, "Boiler repair", SitesTestConstants.SiteId);
        var atArchivedSite = InMemoryClosedDays.On(ClosedWednesday, "Flood", SitesTestConstants.OtherSiteId);
        repository.Setup(r => r.GetAsync(null, null, cts.Token)).ReturnsAsync(new List<ClosedDay> { CreateClosedDay(), atSite, atArchivedSite });
        sites.Setup(r => r.GetSitesAsync(true, cts.Token)).ReturnsAsync(
        [
            SitesTestConstants.NewSite(),
            SitesTestConstants.NewSite(SitesTestConstants.OtherSiteId, SitesTestConstants.OtherSiteName, isArchived: true)
        ]);
        fixture.Inject(repository.Object);
        fixture.Inject(sites.Object);
        fixture.Inject<TimeProvider>(new FixedTimeProvider(Now));

        var response = await fixture.Create<GetClosedDaysHandler>().Handle(new GetClosedDaysQuery(null, null), cts.Token);

        response.Select(d => d.SiteName).Should().Equal(null, SitesTestConstants.SiteName, SitesTestConstants.OtherSiteName);
        response.Select(d => d.SiteId).Should().Equal(null, SitesTestConstants.SiteId, SitesTestConstants.OtherSiteId);
    }
}
