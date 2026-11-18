using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.UnitTests.Common.Fixtures;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Fixtures.InMemoryWorkingHours;

namespace MedApp.UnitTests.Application.Calendar.Common;

/// <summary>
/// A person's working day (<see cref="WorkingTimeSet.For"/>): their hours or the default, inside their site's, less
/// closed days (centre plan decisions 119 to 122).
/// </summary>
[TestFixture]
public sealed class WorkingTimeTests
{
    private static readonly Guid SiteId = Guid.Parse("5173e000-0000-4000-8000-0000000000c1");
    private static readonly TimeOnly Nine = new(9, 0);
    private static readonly TimeOnly One = new(13, 0);
    private static readonly TimeOnly Eight = new(8, 0);
    private static readonly TimeOnly Twenty = new(20, 0);

    private static WorkingTime For(WorkingTimeSet set, DateOnly date, ClosedDaySet? closed = null) =>
        set.For(StaffUserId, date, closed ?? ClosedDaySet.None);

    [Test]
    public void For_NoHoursOfTheirOwn_IsTheDefaultDay_AtNoSite()
    {
        var monday = For(WorkingTimeSet.None, NextMonday);

        monday.Should().BeEquivalentTo(new WorkingTime(NextMonday, WorkingHours.Start, WorkingHours.End, null, null, false));
        monday.IsWorking.Should().BeTrue();
        monday.StartUtc.Should().Be(WorkingHours.DayStartUtc(NextMonday));
        monday.EndUtc.Should().Be(WorkingHours.DayEndUtc(NextMonday));

        var saturday = For(WorkingTimeSet.None, NextSaturday);
        saturday.HasHours.Should().BeFalse();
        saturday.IsWorking.Should().BeFalse();
        saturday.WhyNotWorking().Should().Be(WorkingHours.NotWorking(DayOfWeek.Saturday));
        saturday.StartUtc.Should().Be(WorkingHours.DayStartUtc(NextSaturday), "a day not worked shows the default's hours");
    }

    [Test]
    public void For_TheirOwnHours_WorksOnlyTheDaysTheyName_AtTheirSite()
    {
        var set = new WorkingTimeSet(
            [StaffRow(StaffUserId, DayOfWeek.Saturday, Nine, One, SiteId)],
            [SiteRow(SiteId, DayOfWeek.Saturday, Eight, One)]);

        var saturday = For(set, NextSaturday);
        saturday.Should().BeEquivalentTo(new WorkingTime(NextSaturday, Nine, One, SiteId, null, true));
        saturday.IsWorking.Should().BeTrue();

        var monday = For(set, NextMonday);
        monday.HasHours.Should().BeFalse("their own hours don't name Monday");
        monday.HasOwnHours.Should().BeTrue();
        monday.WhyNotWorking("k.ashford").Should().Be(WorkingHours.NotWorking(DayOfWeek.Monday, "k.ashford"));
    }

    [Test]
    public void For_SomeoneElsesHours_DontCount()
    {
        var set = new WorkingTimeSet([StaffRow(OtherStaffUserId, DayOfWeek.Saturday, Nine, One, SiteId)], []);

        For(set, NextSaturday).HasHours.Should().BeFalse();
        For(set, NextMonday).Should().BeEquivalentTo(new WorkingTime(NextMonday, WorkingHours.Start, WorkingHours.End, null, null, false));
    }

    [Test]
    public void For_AnEveningAtASiteWithTheDefaultHours_IsCutToTheSitesHours()
    {
        // The site has no hours of its own, so it's open 08:00 to 18:00 on weekdays.
        var set = new WorkingTimeSet([StaffRow(StaffUserId, DayOfWeek.Tuesday, Nine, Twenty, SiteId)], []);

        var tuesday = For(set, NextMonday.AddDays(1));
        tuesday.Starts.Should().Be(Nine);
        tuesday.Ends.Should().Be(WorkingHours.End);
    }

    [Test]
    public void For_ASiteOpenUntil2000OnTuesdays_KeepsTheirEvening()
    {
        var set = new WorkingTimeSet(
            [StaffRow(StaffUserId, DayOfWeek.Tuesday, Nine, Twenty, SiteId)],
            [SiteRow(SiteId, DayOfWeek.Tuesday, Eight, Twenty)]);

        var tuesday = For(set, NextMonday.AddDays(1));
        tuesday.Starts.Should().Be(Nine);
        tuesday.Ends.Should().Be(Twenty);
        tuesday.WhyOutside(At(tuesday.Date, 19), At(tuesday.Date, 20)).Should().BeNull();
        tuesday.WhyOutside(At(tuesday.Date, 8, 30), At(tuesday.Date, 9, 30)).Should().Be(WorkingHours.Within(Nine, Twenty));
        tuesday.WhyOutside(At(tuesday.Date, 8, 30), At(tuesday.Date, 9, 30), "k.ashford")
            .Should().Be(WorkingHours.Within(Nine, Twenty, "k.ashford"));
    }

    [Test]
    public void For_ASiteClosedThatWeekday_MeansTheyDontWork()
    {
        var set = new WorkingTimeSet(
            [StaffRow(StaffUserId, DayOfWeek.Saturday, Nine, One, SiteId)],
            [SiteRow(SiteId, DayOfWeek.Monday, Eight, One)]);

        For(set, NextSaturday).HasHours.Should().BeFalse();
        For(set, NextSaturday).SiteId.Should().Be(SiteId);
    }

    [Test]
    public void For_ASitesClosedDay_ClosesTheirDay_ButNotTheDefaults()
    {
        var closed = new ClosedDaySet([InMemoryClosedDays.On(ClosedWednesday, "Staff training", SiteId)]);
        var set = new WorkingTimeSet([StaffRow(StaffUserId, DayOfWeek.Wednesday, Nine, One, SiteId)], []);

        var theirs = For(set, ClosedWednesday, closed);
        theirs.HasHours.Should().BeTrue();
        theirs.IsWorking.Should().BeFalse();
        theirs.ClosedDayName.Should().Be("Staff training");
        theirs.WhyOutside(At(ClosedWednesday, 10), At(ClosedWednesday, 11))
            .Should().Be(WorkingHours.ClosedDay(ClosedWednesday, "Staff training"));
        theirs.WhyOutside(At(ClosedWednesday, 10), At(ClosedWednesday, 11), closedDaysCount: false)
            .Should().BeNull("a walk-in may be on a closed day");

        For(WorkingTimeSet.None, ClosedWednesday, closed).IsWorking.Should().BeTrue("someone at no site isn't closed by a site's day");
    }

    [Test]
    public void For_ADayClosedEverywhere_ClosesEveryone()
    {
        var closed = new ClosedDaySet([CreateClosedDay()]);
        var set = new WorkingTimeSet([StaffRow(StaffUserId, DayOfWeek.Wednesday, Nine, One, SiteId)], []);

        For(set, ClosedWednesday, closed).ClosedDayName.Should().Be(ClosedDayName);
        For(WorkingTimeSet.None, ClosedWednesday, closed).ClosedDayName.Should().Be(ClosedDayName);
    }

    [Test]
    public void For_DatedRows_ApplyOnlyWhileValid()
    {
        var set = new WorkingTimeSet(
            [StaffRow(StaffUserId, DayOfWeek.Monday, Nine, One, SiteId, validFrom: NextMonday)],
            []);

        For(set, Today).Should().BeEquivalentTo(new WorkingTime(Today, WorkingHours.Start, WorkingHours.End, null, null, false),
            "before their own hours start, they worked the default");
        For(set, NextMonday).Starts.Should().Be(Nine);
    }

    [Test]
    public void WhyOutside_InsideTheirDay_IsNull_AndOnANonWorkingDay_SaysSo()
    {
        For(WorkingTimeSet.None, NextMonday).WhyOutside(At(NextMonday, 8), At(NextMonday, 18)).Should().BeNull();
        For(WorkingTimeSet.None, NextMonday).WhyOutside(At(NextMonday, 17), At(NextMonday, 18, 30)).Should().Be(WorkingHours.OutsideWorkingDay);
        For(WorkingTimeSet.None, NextSunday).WhyOutside(At(NextSunday, 10), At(NextSunday, 11))
            .Should().Be(WorkingHours.NotWorking(DayOfWeek.Sunday));
    }

    [Test]
    public void SiteOpening_IsTheDefaultsWithoutHours_AndTheirsWithThem()
    {
        WorkingTimeSet.None.SiteOpening(SiteId, NextMonday).Should().Be((WorkingHours.Start, WorkingHours.End));
        WorkingTimeSet.None.SiteOpening(SiteId, NextSaturday).Should().BeNull();

        var set = new WorkingTimeSet([], [SiteRow(SiteId, DayOfWeek.Saturday, Eight, One)]);
        set.SiteOpening(SiteId, NextSaturday).Should().Be((Eight, One));
        set.SiteOpening(SiteId, NextMonday).Should().BeNull("with hours of its own, it's open only on the days they name");
    }
}
