using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Domain.Calendar;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Fixtures.InMemoryWorkingHours;

namespace MedApp.UnitTests.Application.Calendar.Common;

/// <summary>
/// The person's working day where calendars use it (centre plan decision 120): the days shown and their grid
/// (<see cref="CalendarProjection"/>, <see cref="GridWindow"/>), templates, clashes and free time.
/// </summary>
[TestFixture]
public sealed class WorkingHoursOnCalendarsTests
{
    private static readonly Guid SiteId = Guid.Parse("5173e000-0000-4000-8000-0000000000c1");
    private static readonly TimeOnly Nine = new(9, 0);
    private static readonly TimeOnly One = new(13, 0);

    /// <summary>Works Saturday mornings, 09:00 to 13:00, at a site open then, and the default nowhere else.</summary>
    private static WorkingTimeSet SaturdayMornings(Guid? staffUserId = null) => new(
        [StaffRow(staffUserId ?? StaffUserId, DayOfWeek.Saturday, Nine, One, SiteId)],
        [SiteRow(SiteId, DayOfWeek.Saturday, new TimeOnly(8, 0), One)]);

    private static CalendarData Week(WorkingTimeSet hours, IEnumerable<CalendarBlock>? blocks = null) =>
        CreateData(NextMonday, NextSunday, blocks: blocks) with { Hours = hours };

    [Test]
    public void Days_OnTheDefault_AreAsBefore()
    {
        var days = CalendarProjection.Days(CreateData(NextMonday, NextSunday), Today);

        days.Select(d => d.IsWorkingDay).Should().Equal(true, true, true, true, true, false, false);
        days.Should().AllSatisfy(d =>
        {
            d.WorkStartUtc.Should().Be(WorkingHours.DayStartUtc(d.Date));
            d.WorkEndUtc.Should().Be(WorkingHours.DayEndUtc(d.Date));
            d.GridStartUtc.Should().Be(d.WorkStartUtc);
            d.GridEndUtc.Should().Be(d.WorkEndUtc);
            d.SiteId.Should().BeNull();
        });
        days.Select(d => d.HasHours).Should().Equal(true, true, true, true, true, false, false);
    }

    [Test]
    public void Days_ASaturdayWorker_WorksSaturday_AndNoWeekday()
    {
        var days = CalendarProjection.Days(Week(SaturdayMornings()), Today);

        days.Select(d => d.IsWorkingDay).Should().Equal(false, false, false, false, false, true, false);
        var saturday = days[5];
        saturday.WorkStartUtc.Should().Be(At(NextSaturday, 9));
        saturday.WorkEndUtc.Should().Be(At(NextSaturday, 13));
        saturday.SiteId.Should().Be(SiteId);
        saturday.GridStartUtc.Should().Be(At(NextSaturday, 9), "the grid fits the hours worked that week");
        saturday.GridEndUtc.Should().Be(At(NextSaturday, 13));
        days[0].GridStartUtc.Should().Be(At(NextMonday, 9), "every day of one calendar has the same grid");
    }

    [Test]
    public void TemplateBlocks_OnlyOnTheDaysTheyWork()
    {
        var data = Week(SaturdayMornings()) with
        {
            DayOverrides = [CreateOverride(NextSaturday, DayTemplateId)],
            DayTemplateBlocks = [CreateTemplateBlock(10, 11)]
        };

        CalendarProjection.TemplateBlocks(data).Select(b => b.Date).Should().Equal(NextSaturday);
    }

    [Test]
    public void GridWindow_WithNothing_IsTheDefaultDay()
    {
        GridWindow.Of([]).Should().Be(GridWindow.Default);
        GridWindow.Of([CreateData(NextSaturday, NextSunday)]).Should().Be(GridWindow.Default);
    }

    [Test]
    public void GridWindow_FitsTheLatestEnd_AndWhatsOnTheCalendar_InWholeHours()
    {
        var evening = new WorkingTimeSet(
            [StaffRow(StaffUserId, DayOfWeek.Tuesday, new TimeOnly(8, 0), new TimeOnly(19, 30), SiteId)],
            [SiteRow(SiteId, DayOfWeek.Tuesday, new TimeOnly(8, 0), new TimeOnly(20, 0))]);
        GridWindow.Of([Week(evening)]).Should().Be(new GridWindow(new TimeOnly(8, 0), new TimeOnly(20, 0)));

        // Something on the calendar outside the hours (put there before they changed) still shows.
        var early = Week(SaturdayMornings(), blocks: [CreateBlock(At(NextMonday, 7, 30), At(NextMonday, 8))]);
        GridWindow.Of([early]).Should().Be(new GridWindow(new TimeOnly(7, 0), new TimeOnly(13, 0)));
    }

    [Test]
    public void TeamDay_IsWorkedWhenEveryoneWorks_FromTheLatestStartToTheEarliestEnd()
    {
        var saturdayWorker = CreateData(NextSaturday) with { Hours = SaturdayMornings() };
        var onTheDefault = CreateData(NextSaturday) with { StaffUserId = OtherStaffUserId };

        var both = CalendarProjection.TeamDay([saturdayWorker, onTheDefault], NextSaturday, Today, ClosedDaySet.None, GridWindow.Default);
        both.IsWorkingDay.Should().BeFalse("one of them doesn't work Saturdays");

        var one = CalendarProjection.TeamDay([saturdayWorker], NextSaturday, Today, ClosedDaySet.None, GridWindow.Default);
        one.IsWorkingDay.Should().BeTrue();
        one.WorkStartUtc.Should().Be(At(NextSaturday, 9));
        one.WorkEndUtc.Should().Be(At(NextSaturday, 13));

        CalendarProjection.TeamDay([], NextMonday, Today, ClosedDaySet.None, GridWindow.Default).IsWorkingDay.Should().BeTrue();
    }

    [Test]
    public void Find_OutsideTheirHours_SaysSo_BeforeAnyClash()
    {
        var data = CreateData(NextSaturday) with { Hours = SaturdayMornings() };

        CalendarClashes.Find(data, At(NextSaturday, 10), At(NextSaturday, 11)).Should().BeNull();
        CalendarClashes.Find(data, At(NextSaturday, 12), At(NextSaturday, 14)).Should().Be(WorkingHours.Within(Nine, One));
        CalendarClashes.Find(CreateData(NextSaturday), At(NextSaturday, 10), At(NextSaturday, 11))
            .Should().Be(WorkingHours.NotWorking(DayOfWeek.Saturday));
    }

    [Test]
    public void FreeTime_IsInsideEveryonesHours()
    {
        var saturdayWorker = CreateData(NextSaturday) with { Hours = SaturdayMornings() };
        var anotherSaturdayWorker = CreateData(NextSaturday) with
        {
            StaffUserId = OtherStaffUserId,
            Hours = new WorkingTimeSet(
                [StaffRow(OtherStaffUserId, DayOfWeek.Saturday, new TimeOnly(10, 0), One, SiteId)],
                [SiteRow(SiteId, DayOfWeek.Saturday, new TimeOnly(8, 0), One)])
        };

        var free = Availability.FreeTime([saturdayWorker, anotherSaturdayWorker], NextSaturday, NextSaturday, Now);

        free.Should().ContainSingle().Which.Free.Should().Equal(new TimeRange(At(NextSaturday, 10), At(NextSaturday, 13)));
        Availability.FreeTime([saturdayWorker, CreateData(NextSaturday) with { StaffUserId = OtherStaffUserId }], NextSaturday, NextSaturday, Now)
            .Should().BeEmpty("one of them doesn't work Saturdays");
    }
}
