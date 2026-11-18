using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Hours.Queries.GetStaffHours;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Sites.Repositories;
using MedApp.Domain.Sites;
using MedApp.UnitTests.Application.Calendar.StaffCalendars;
using MedApp.UnitTests.Common.Constants;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Fixtures.InMemoryWorkingHours;

namespace MedApp.UnitTests.Application.Calendar.Hours.Queries.GetStaffHours;

[TestFixture]
public sealed class GetStaffHoursHandlerTests : StaffCalendarHandlerTestBase
{
    private static readonly Site Site = SitesTestConstants.NewSite();
    private GetStaffHoursHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        var sites = new Mock<ISiteRepository>();
        sites.Setup(r => r.GetSitesAsync(true, It.IsAny<CancellationToken>())).ReturnsAsync([Site]);
        Fixture.Inject(sites.Object);
        _handler = Fixture.Create<GetStaffHoursHandler>();
    }

    [Test]
    public async Task Handle_TheirOwnHoursToday_WithTheSitesNames_ReadOnlyForStaff()
    {
        Hours.Staff.Add(StaffRow(StaffUserId, DayOfWeek.Saturday, new TimeOnly(9, 0), new TimeOnly(13, 0), Site.Id, validFrom: LongAgo));
        Hours.Staff.Add(StaffRow(StaffUserId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0), Site.Id, validFrom: LongAgo));
        Hours.Staff.Add(StaffRow(StaffUserId, DayOfWeek.Friday, new TimeOnly(9, 0), new TimeOnly(17, 0), Site.Id, validFrom: LongAgo, validTo: Today.AddDays(-1)));

        var response = await _handler.Handle(new GetStaffHoursQuery(StaffUserId, StaffActor), Token);

        response.HasOwnHours.Should().BeTrue();
        response.CanEdit.Should().BeFalse();
        response.Since.Should().Be(LongAgo);
        response.Days.Select(d => (d.DayOfWeek, d.SiteName)).Should().Equal(("Monday", Site.Name), ("Saturday", Site.Name));
    }

    [Test]
    public async Task Handle_NoHoursOfTheirOwn_IsTheDefault_EditableByAnAdmin()
    {
        var response = await _handler.Handle(new GetStaffHoursQuery(StaffUserId, AdminActor), Token);

        response.HasOwnHours.Should().BeFalse();
        response.Since.Should().BeNull();
        response.CanEdit.Should().BeTrue();
        response.Days.Should().HaveCount(5);
    }

    [Test]
    public async Task Handle_SomeoneWhoIsntStaff_Is404()
    {
        var act = () => _handler.Handle(new GetStaffHoursQuery(Guid.NewGuid(), StaffActor), Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
