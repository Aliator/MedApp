using FluentAssertions;
using MedApp.Application.Jobs.Common;

namespace MedApp.UnitTests.Application.Jobs.Common;

[TestFixture]
public sealed class JobScheduleTests
{
    private static DateTime Utc(int year, int month, int day, int hour, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    [Test]
    public void Every_LessThanAMinute_Throws()
    {
        var act = () => JobSchedule.Every(TimeSpan.FromSeconds(59));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestCase(1, 0)]
    [TestCase(2, 30)]
    [TestCase(2, 59)]
    public void DailyAt_DuringTheClockChangeHours_Throws(int hour, int minute)
    {
        var act = () => JobSchedule.DailyAt(new TimeOnly(hour, minute));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestCase(0, 59)]
    [TestCase(3, 0)]
    [TestCase(23, 0)]
    public void DailyAt_OutsideTheClockChangeHours_IsAllowed(int hour, int minute)
    {
        JobSchedule.DailyAt(new TimeOnly(hour, minute)).Interval.Should().Be(TimeSpan.FromDays(1));
    }

    [Test]
    public void NextAfter_Interval_AddsIt()
    {
        JobSchedule.Every(TimeSpan.FromMinutes(15)).NextAfter(Utc(2026, 11, 18, 10)).Should().Be(Utc(2026, 11, 18, 10, 15));
    }

    [Test]
    public void NextAfter_DailyBeforeTodaysTime_IsToday()
    {
        // Winter: UK time is UTC.
        JobSchedule.DailyAt(new TimeOnly(4, 30)).NextAfter(Utc(2026, 11, 18, 3)).Should().Be(Utc(2026, 11, 18, 4, 30));
    }

    [Test]
    public void NextAfter_DailyAtOrAfterTodaysTime_IsTomorrow()
    {
        var schedule = JobSchedule.DailyAt(new TimeOnly(4, 30));

        schedule.NextAfter(Utc(2026, 11, 18, 4, 30)).Should().Be(Utc(2026, 11, 19, 4, 30));
        schedule.NextAfter(Utc(2026, 11, 18, 10)).Should().Be(Utc(2026, 11, 19, 4, 30));
    }

    [Test]
    public void NextAfter_DailyInSummer_IsTheUkClockTime()
    {
        // British Summer Time: 04:30 UK is 03:30 UTC.
        JobSchedule.DailyAt(new TimeOnly(4, 30)).NextAfter(Utc(2026, 7, 1, 12)).Should().Be(Utc(2026, 7, 2, 3, 30));
    }

    [Test]
    public void NextAfter_DailyAcrossTheSpringClockChange_KeepsTheUkClockTime()
    {
        // The clocks go forward on Sunday 29 March 2026: 04:30 is 04:30 UTC on Saturday, 03:30 UTC on Sunday.
        var schedule = JobSchedule.DailyAt(new TimeOnly(4, 30));

        schedule.NextAfter(Utc(2026, 3, 28, 4, 30)).Should().Be(Utc(2026, 3, 29, 3, 30));
    }

    [Test]
    public void FirstRun_Interval_IsNow()
    {
        JobSchedule.Every(TimeSpan.FromHours(1)).FirstRun(Utc(2026, 11, 18, 10)).Should().Be(Utc(2026, 11, 18, 10));
    }

    [Test]
    public void FirstRun_Daily_IsTheNextDailyTime()
    {
        JobSchedule.DailyAt(new TimeOnly(4, 30)).FirstRun(Utc(2026, 11, 18, 10)).Should().Be(Utc(2026, 11, 19, 4, 30));
    }

    [Test]
    public void NextRunAfter_Success_IsTheNextAfterTheStart()
    {
        var schedule = JobSchedule.Every(TimeSpan.FromMinutes(15));

        schedule.NextRunAfter(Utc(2026, 11, 18, 10), Utc(2026, 11, 18, 10, 2), failed: false)
            .Should().Be(Utc(2026, 11, 18, 10, 15));
    }

    [Test]
    public void NextRunAfter_SuccessThatRanPastTheNext_IsTheNextAfterTheEnd()
    {
        var schedule = JobSchedule.Every(TimeSpan.FromMinutes(15));

        schedule.NextRunAfter(Utc(2026, 11, 18, 10), Utc(2026, 11, 18, 10, 20), failed: false)
            .Should().Be(Utc(2026, 11, 18, 10, 35));
    }

    [Test]
    public void NextRunAfter_FailureOfADailyJob_RetriesInHalfAnHour()
    {
        var schedule = JobSchedule.DailyAt(new TimeOnly(4, 30));

        schedule.NextRunAfter(Utc(2026, 11, 18, 4, 30), Utc(2026, 11, 18, 4, 31), failed: true)
            .Should().Be(Utc(2026, 11, 18, 5, 1));
    }

    [Test]
    public void NextRunAfter_FailureOfAFrequentJob_RetriesAtItsInterval()
    {
        var schedule = JobSchedule.Every(TimeSpan.FromMinutes(5));

        schedule.NextRunAfter(Utc(2026, 11, 18, 10), Utc(2026, 11, 18, 10, 1), failed: true)
            .Should().Be(Utc(2026, 11, 18, 10, 6));
    }

    [Test]
    public void IsOverdue_MoreThanTwoIntervalsLate_IsTrue()
    {
        var schedule = JobSchedule.Every(TimeSpan.FromMinutes(15));
        var due = Utc(2026, 11, 18, 10);

        schedule.IsOverdue(due, due.AddMinutes(30)).Should().BeFalse();
        schedule.IsOverdue(due, due.AddMinutes(31)).Should().BeTrue();
    }

    [Test]
    public void Describe_SaysTheScheduleInWords()
    {
        JobSchedule.DailyAt(new TimeOnly(4, 30)).Describe().Should().Be("Daily at 04:30 (UK time)");
        JobSchedule.Every(TimeSpan.FromMinutes(1)).Describe().Should().Be("Every minute");
        JobSchedule.Every(TimeSpan.FromMinutes(15)).Describe().Should().Be("Every 15 minutes");
        JobSchedule.Every(TimeSpan.FromMinutes(90)).Describe().Should().Be("Every 90 minutes");
        JobSchedule.Every(TimeSpan.FromHours(1)).Describe().Should().Be("Every hour");
        JobSchedule.Every(TimeSpan.FromHours(6)).Describe().Should().Be("Every 6 hours");
        JobSchedule.Every(TimeSpan.FromDays(2)).Describe().Should().Be("Every 2 days");
    }
}
