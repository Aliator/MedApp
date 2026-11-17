using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.UnitTests.Common.Fixtures;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Common;

[TestFixture]
public sealed class UkTimeTests
{
    [Test]
    public void ToUtc_WinterDate_IsTheSameClockTime()
    {
        UkTime.ToUtc(NextMonday, new TimeOnly(10, 0))
            .Should().Be(new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void ToUtc_SummerDate_IsAnHourEarlier()
    {
        UkTime.ToUtc(SummerMonday, new TimeOnly(10, 0))
            .Should().Be(new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void ToUtc_EitherSideOfTheClockChange_KeepsTheUkClockTime()
    {
        UkTime.ToUtc(FridayBeforeClockChange, new TimeOnly(12, 0))
            .Should().Be(new DateTime(2026, 10, 23, 11, 0, 0, DateTimeKind.Utc));
        UkTime.ToUtc(MondayAfterClockChange, new TimeOnly(12, 0))
            .Should().Be(new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void ToUtc_Result_IsMarkedUtc()
    {
        UkTime.ToUtc(NextMonday, new TimeOnly(10, 0)).Kind.Should().Be(DateTimeKind.Utc);
    }

    [Test]
    public void ToUk_SummerMoment_IsAnHourLater()
    {
        UkTime.ToUk(new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc))
            .Should().Be(new DateTime(2026, 6, 15, 10, 0, 0));
    }

    [Test]
    public void DateOf_LateEveningUtcInSummer_IsTheNextUkDate()
    {
        UkTime.DateOf(new DateTime(2026, 6, 15, 23, 30, 0, DateTimeKind.Utc)).Should().Be(new DateOnly(2026, 6, 16));
    }

    [Test]
    public void DateOf_LateEveningUtcInWinter_IsTheSameDate()
    {
        UkTime.DateOf(new DateTime(2026, 11, 23, 23, 30, 0, DateTimeKind.Utc)).Should().Be(NextMonday);
    }

    [Test]
    public void TimeOf_SummerMoment_IsTheUkClockTime()
    {
        UkTime.TimeOf(new DateTime(2026, 6, 15, 9, 15, 0, DateTimeKind.Utc)).Should().Be(new TimeOnly(10, 15));
    }

    [Test]
    public void Today_JustBeforeMidnightUtcInSummer_IsTheNextUkDate()
    {
        var clock = new FixedTimeProvider(new DateTime(2026, 6, 15, 23, 30, 0, DateTimeKind.Utc));

        UkTime.Today(clock).Should().Be(new DateOnly(2026, 6, 16));
    }

    [Test]
    public void Describe_WinterMoment_SaysTheDayDateAndTime()
    {
        UkTime.Describe(new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc)).Should().Be("Monday 23 November at 10:00");
    }

    [Test]
    public void Describe_SummerMoment_SaysTheUkTime()
    {
        UkTime.Describe(new DateTime(2026, 6, 15, 9, 5, 0, DateTimeKind.Utc)).Should().Be("Monday 15 June at 10:05");
    }

    [Test]
    public void DescribeTime_Moment_IsTheUkClockTime()
    {
        UkTime.DescribeTime(new DateTime(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc)).Should().Be("09:00");
    }

    [Test]
    public void DescribeRange_WinterTimes_SaysTheDayDateAndBothTimes()
    {
        UkTime.DescribeRange(
                new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 11, 23, 11, 30, 0, DateTimeKind.Utc))
            .Should().Be("Monday 23 November, 10:00 to 11:30");
    }

    [Test]
    public void DescribeRange_SummerTimes_SaysTheUkTimes()
    {
        UkTime.DescribeRange(
                new DateTime(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 15, 9, 5, 0, DateTimeKind.Utc))
            .Should().Be("Monday 15 June, 09:00 to 10:05");
    }

    [Test]
    public void DescribeDate_Date_SaysTheDayAndDate()
    {
        UkTime.DescribeDate(NextMonday).Should().Be("Monday 23 November");
    }

    [Test]
    public void Utc_UtcTime_IsUnchanged()
    {
        var time = new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);

        UkTime.Utc(time).Should().Be(time);
        UkTime.Utc(time).Kind.Should().Be(DateTimeKind.Utc);
    }

    [Test]
    public void Utc_UnspecifiedTime_IsTakenAsUtc()
    {
        var result = UkTime.Utc(new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Unspecified));

        result.Should().Be(new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc));
        result.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Test]
    public void Utc_LocalTime_IsConvertedToUtc()
    {
        var local = new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Local);

        var result = UkTime.Utc(local);

        result.Should().Be(local.ToUniversalTime());
        result.Kind.Should().Be(DateTimeKind.Utc);
    }
}
