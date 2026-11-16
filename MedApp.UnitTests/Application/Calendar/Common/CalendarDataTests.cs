using FluentAssertions;
using MedApp.Application.Calendar.Common;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Common;

[TestFixture]
public sealed class CalendarDataTests
{
    [Test]
    public void Empty_HasTheRangeAndNothingOnIt()
    {
        var data = CalendarData.Empty(StaffUserId, NextMonday, NextFriday);

        data.StaffUserId.Should().Be(StaffUserId);
        data.From.Should().Be(NextMonday);
        data.To.Should().Be(NextFriday);
        data.StaffWeekTemplates.Should().BeEmpty();
        data.WeekTemplateDays.Should().BeEmpty();
        data.DayTemplateBlocks.Should().BeEmpty();
        data.DayOverrides.Should().BeEmpty();
        data.BlockExceptions.Should().BeEmpty();
        data.Blocks.Should().BeEmpty();
        data.Appointments.Should().BeEmpty();
        data.DayTemplateNames.Should().BeEmpty();
    }

    [Test]
    public void Empty_ProjectsWorkingDaysWithoutTemplatesOrBlocks()
    {
        var data = CalendarData.Empty(StaffUserId, NextMonday, NextSunday);

        CalendarProjection.TemplateBlocks(data).Should().BeEmpty();
        CalendarProjection.Days(data, Today).Should().HaveCount(7).And.OnlyContain(d => d.DayTemplateId == null);
    }
}
