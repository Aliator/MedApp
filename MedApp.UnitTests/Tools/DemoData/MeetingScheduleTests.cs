using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.DevMcp.DemoData;
using MedApp.Domain.Calendar;
using static MedApp.UnitTests.Common.Constants.DemoDataTestConstants;

namespace MedApp.UnitTests.Tools.DemoData;

/// <summary>The demo meetings' date arithmetic (<see cref="MeetingSchedule"/>).</summary>
[TestFixture]
public sealed class MeetingScheduleTests
{
    [Test]
    public void Weekly_FromMidweek_StartsOnTheNextOfThatWeekday_AndIncludesTheLastDay()
    {
        var dates = MeetingSchedule.Weekly(Monday.AddDays(2), Monday.AddDays(25), DayOfWeek.Monday).ToList();

        dates.Should().Equal(Monday.AddDays(7), Monday.AddDays(14), Monday.AddDays(21));
    }

    [Test]
    public void Weekly_FromThatWeekday_IncludesIt() =>
        MeetingSchedule.Weekly(Monday, Monday.AddDays(7), DayOfWeek.Monday).Should().Equal(Monday, Monday.AddDays(7));

    [Test]
    public void MonthlyNth_SecondWednesday_IsBetweenThe8thAndThe14th()
    {
        var dates = MeetingSchedule.MonthlyNth(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), DayOfWeek.Wednesday, 2).ToList();

        dates.Should().HaveCount(12).And.OnlyContain(d => d.DayOfWeek == DayOfWeek.Wednesday && d.Day >= 8 && d.Day <= 14);
        dates.Should().Contain(new DateOnly(2026, 11, 11));
    }

    [Test]
    public void MonthlyNth_LeavesOutDatesBeforeTheStart()
    {
        // The second Wednesday of November 2026 is the 11th: from the 12th, the first is December's.
        MeetingSchedule.MonthlyNth(new DateOnly(2026, 11, 12), new DateOnly(2026, 12, 31), DayOfWeek.Wednesday, 2)
            .Should().Equal(new DateOnly(2026, 12, 9));
    }

    [TestCase(0)]
    [TestCase(5)]
    public void MonthlyNth_NotOneToFour_Throws(int nth) =>
        FluentActions.Invoking(() => MeetingSchedule.MonthlyNth(Monday, Monday.AddDays(60), DayOfWeek.Monday, nth).ToList())
            .Should().Throw<ArgumentOutOfRangeException>();

    [TestCase(0, -4)]
    [TestCase(7, -4)]
    [TestCase(8, 4)]
    [TestCase(15, 4)]
    [TestCase(16, 12)]
    public void BatchArrangedOn_IsFourWeeksBeforeItsBatchOfEight(int weeksIn, int arrangedWeek)
    {
        var date = Monday.AddDays(7 * weeksIn + 4);

        MeetingSchedule.BatchArrangedOn(date, Monday).Should().Be(Monday.AddDays(7 * arrangedWeek));
    }

    [Test]
    public void BatchArrangedOn_BeforeTheFirstMonday_Throws() =>
        FluentActions.Invoking(() => MeetingSchedule.BatchArrangedOn(Monday.AddDays(-1), Monday))
            .Should().Throw<ArgumentOutOfRangeException>();

    [Test]
    public void TrainingTitle_GoesRoundTheTopicsInTurn()
    {
        var count = MeetingSchedule.TrainingTitles.Length;

        MeetingSchedule.TrainingTitle(0).Should().Be(MeetingSchedule.TrainingTitles[0]);
        MeetingSchedule.TrainingTitle(count + 1).Should().Be(MeetingSchedule.TrainingTitles[1]);
        MeetingSchedule.TrainingTitles.Should().OnlyHaveUniqueItems().And.OnlyContain(t => t.Length <= MeetingLimits.MaxTitleLength);
    }

    [Test]
    public void Between_IsAfterTheFirstAndBeforeTheSecond()
    {
        var between = MeetingSchedule.Between(Now, Now.AddHours(4));

        between.Should().Be(Now.AddHours(1));
    }

    [Test]
    public void FirstFreeTime_NobodyBusy_IsTheFirstQuarterHourFromTheClockTime()
    {
        var time = MeetingSchedule.FirstFreeTime(
            [CalendarData.Empty(StaffId, Monday, Monday.AddDays(1)), CalendarData.Empty(OtherStaffId, Monday, Monday.AddDays(1))],
            [Monday, Monday.AddDays(1)], 60, new TimeOnly(10, 0), Now);

        time.Should().Be(new TimeRange(UkTime.ToUtc(Monday, new(10, 0)), UkTime.ToUtc(Monday, new(11, 0))));
    }

    [Test]
    public void FirstFreeTime_SomeoneBusy_FindsTheFirstTimeEveryoneIsFree()
    {
        var busy = CalendarData.Empty(OtherStaffId, Monday, Monday) with
        {
            Blocks = [new CalendarBlock { Id = Guid.NewGuid(), StaffUserId = OtherStaffId, StartUtc = UkTime.ToUtc(Monday, new(9, 0)), EndUtc = UkTime.ToUtc(Monday, new(10, 40)) }],
        };

        var time = MeetingSchedule.FirstFreeTime([CalendarData.Empty(StaffId, Monday, Monday), busy], [Monday], 30, new TimeOnly(9, 0), Now);

        time!.StartUtc.Should().Be(UkTime.ToUtc(Monday, new(10, 45)), "the first quarter hour after the block, as the app suggests");
    }

    [Test]
    public void FirstFreeTime_OnlyLooksAtTheDatesGiven_AndAfterNow()
    {
        var tuesday = Monday.AddDays(1);
        var thursday = Monday.AddDays(3);
        var data = CalendarData.Empty(StaffId, tuesday, thursday);

        MeetingSchedule.FirstFreeTime([data], [tuesday, thursday], 30, new TimeOnly(8, 0), UkTime.ToUtc(tuesday, new(19, 0)))!
            .StartUtc.Should().Be(UkTime.ToUtc(thursday, new(8, 0)), "Tuesday is over, and Wednesday wasn't asked for");
    }

    [Test]
    public void FirstFreeTime_NoDatesOrNoRoom_IsNull()
    {
        var data = CalendarData.Empty(StaffId, Saturday, Saturday);

        MeetingSchedule.FirstFreeTime([data], [], 30, new TimeOnly(8, 0), Now).Should().BeNull();
        MeetingSchedule.FirstFreeTime([data], [Saturday], 30, new TimeOnly(8, 0), Now).Should().BeNull("nothing is arranged at a weekend");
        MeetingSchedule.FirstFreeTime([CalendarData.Empty(StaffId, Monday, Monday)], [Monday], 60, new TimeOnly(17, 30), Now)
            .Should().BeNull("an hour from 17:30 runs past the working day");
    }
}
