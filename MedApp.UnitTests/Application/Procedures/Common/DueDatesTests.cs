using FluentAssertions;
using MedApp.Application.Procedures.Common;

namespace MedApp.UnitTests.Application.Procedures.Common;

[TestFixture]
public sealed class DueDatesTests
{
    [Test]
    public void EndOfDayUtc_SummerDay_IsTheLastMomentOfTheDayInBritishSummerTime()
    {
        DueDates.EndOfDayUtc(new DateOnly(2026, 6, 15))
            .Should().Be(new DateTime(2026, 6, 15, 22, 59, 59, DateTimeKind.Utc).AddTicks(9_999_999));
    }

    [Test]
    public void EndOfDayUtc_WinterDay_IsTheLastMomentOfTheDayInGreenwichMeanTime()
    {
        DueDates.EndOfDayUtc(new DateOnly(2026, 1, 15))
            .Should().Be(new DateTime(2026, 1, 15, 23, 59, 59, DateTimeKind.Utc).AddTicks(9_999_999));
    }

    [Test]
    public void StartOfDayUtc_SummerDay_IsUkMidnightInUtc()
    {
        DueDates.StartOfDayUtc(new DateOnly(2026, 6, 15))
            .Should().Be(new DateTime(2026, 6, 14, 23, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void ToUkDate_LateEveningUtcInSummer_IsTheNextUkDay()
    {
        DueDates.ToUkDate(new DateTime(2026, 6, 15, 23, 30, 0, DateTimeKind.Utc))
            .Should().Be(new DateOnly(2026, 6, 16));
    }

    [TestCase(2026, 3, 29)]
    [TestCase(2026, 10, 25)]
    [TestCase(2026, 12, 31)]
    public void ToUkDate_EndOfDayUtc_RoundTripsIncludingClockChangeDays(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);

        DueDates.ToUkDate(DueDates.EndOfDayUtc(date)).Should().Be(date);
    }
}
