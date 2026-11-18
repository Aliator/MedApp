using FluentAssertions;
using MedApp.Application.Calendar.Common;
using static MedApp.UnitTests.Common.Fixtures.InMemoryWorkingHours;

namespace MedApp.UnitTests.Application.Calendar.Common;

[TestFixture]
public sealed class WorkingHoursMapperTests
{
    private static readonly Guid StaffUserId = Guid.NewGuid();
    private static readonly Guid SiteId = Guid.NewGuid();
    private static readonly DateOnly Since = new(2026, 11, 2);

    [Test]
    public void ToResponse_StaffHours_MondayFirst_WithSiteNames_AndTheLatestStart()
    {
        var response = WorkingHoursMapper.ToResponse(
            StaffUserId,
            [
                StaffRow(StaffUserId, DayOfWeek.Sunday, new TimeOnly(9, 0), new TimeOnly(12, 0), SiteId, validFrom: Since),
                StaffRow(StaffUserId, DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(16, 0), SiteId, validFrom: Since.AddDays(-7)),
            ],
            new Dictionary<Guid, string> { [SiteId] = "Main site" },
            canEdit: true);

        response.HasOwnHours.Should().BeTrue();
        response.Since.Should().Be(Since);
        response.CanEdit.Should().BeTrue();
        response.Days.Select(d => (d.DayOfWeek, d.SiteName)).Should().Equal(("Monday", "Main site"), ("Sunday", "Main site"));
    }

    [Test]
    public void ToResponse_NoStaffHours_IsTheDefaultWeek_AtNoSite()
    {
        var response = WorkingHoursMapper.ToResponse(StaffUserId, [], new Dictionary<Guid, string>(), canEdit: false);

        response.HasOwnHours.Should().BeFalse();
        response.Since.Should().BeNull();
        response.Days.Select(d => d.DayOfWeek).Should().Equal("Monday", "Tuesday", "Wednesday", "Thursday", "Friday");
        response.Days.Should().AllSatisfy(d =>
        {
            d.Starts.Should().Be(WorkingHours.Start);
            d.Ends.Should().Be(WorkingHours.End);
            d.SiteId.Should().BeNull();
            d.SiteName.Should().BeNull();
        });
    }

    [Test]
    public void ToResponse_SiteHours_AndTheDefault()
    {
        var own = WorkingHoursMapper.ToResponse(SiteId, [SiteRow(SiteId, DayOfWeek.Saturday, new TimeOnly(9, 0), new TimeOnly(13, 0), validFrom: Since)]);
        own.HasOwnHours.Should().BeTrue();
        own.Since.Should().Be(Since);
        own.Days.Should().ContainSingle().Which.Should().BeEquivalentTo(new { DayOfWeek = "Saturday", Opens = new TimeOnly(9, 0), Closes = new TimeOnly(13, 0) });

        var none = WorkingHoursMapper.ToResponse(SiteId, []);
        none.HasOwnHours.Should().BeFalse();
        none.Days.Should().HaveCount(5);
    }

    [TestCase(DayOfWeek.Monday, 0)]
    [TestCase(DayOfWeek.Saturday, 5)]
    [TestCase(DayOfWeek.Sunday, 6)]
    public void Order_IsMondayFirst(DayOfWeek day, int order) => WorkingHoursMapper.Order(day).Should().Be(order);
}
