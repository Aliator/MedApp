using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Sites.Queries.GetSiteHours;
using static MedApp.UnitTests.Common.Constants.SitesTestConstants;
using static MedApp.UnitTests.Common.Fixtures.InMemoryWorkingHours;

namespace MedApp.UnitTests.Application.Sites.Queries.GetSiteHours;

[TestFixture]
public sealed class GetSiteHoursHandlerTests : SiteHandlerTestBase
{
    private GetSiteHoursHandler Handler => Fixture.Create<GetSiteHoursHandler>();

    [Test]
    public async Task Handle_ItsHoursToday_MondayFirst()
    {
        GivenSites(NewSite());
        Hours.Sites.Add(SiteRow(SiteId, DayOfWeek.Saturday, new TimeOnly(9, 0), new TimeOnly(13, 0)));
        Hours.Sites.Add(SiteRow(SiteId, DayOfWeek.Tuesday, new TimeOnly(8, 0), new TimeOnly(20, 0)));
        Hours.Sites.Add(SiteRow(OtherSiteId, DayOfWeek.Sunday, new TimeOnly(8, 0), new TimeOnly(20, 0)));

        var response = await Handler.Handle(new GetSiteHoursQuery(SiteId), Token);

        response.SiteId.Should().Be(SiteId);
        response.HasOwnHours.Should().BeTrue();
        response.Days.Select(d => d.DayOfWeek).Should().Equal("Tuesday", "Saturday");
    }

    [Test]
    public async Task Handle_AnUnknownSite_Is404()
    {
        var act = () => Handler.Handle(new GetSiteHoursQuery(SiteId), Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
