using FluentAssertions;
using MedApp.Application.Procedures.Insights.Common;
using static MedApp.UnitTests.Common.Constants.InsightsTestConstants;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.Insights.Common;

[TestFixture]
public sealed class InsightsPeriodTests
{
    [Test]
    public void For_Weeks_EndsWithThisUkWeekStartingMondayMidnightUkTime()
    {
        var period = InsightsPeriod.For(Now, 4);

        period.Weeks.Select(w => w.Monday).Should().Equal(
            ThisMonday.AddDays(-21), ThisMonday.AddDays(-14), ThisMonday.AddDays(-7), ThisMonday);
        period.ThisWeek.StartUtc.Should().Be(ThisWeekStartUtc);
        period.ThisWeek.EndUtc.Should().Be(ThisWeekStartUtc.AddDays(7));
        period.StartUtc.Should().Be(ThisWeekStartUtc.AddDays(-21));
        period.NowUtc.Should().Be(Now);
    }

    [Test]
    public void For_SundayEveningUtcInSummer_IsAlreadyTheNextUkWeek()
    {
        // 23:30 UTC on Sunday is 00:30 BST on Monday.
        var period = InsightsPeriod.For(ThisWeekStartUtc.AddMinutes(30), 1);

        period.ThisWeek.Monday.Should().Be(ThisMonday);
    }

    [Test]
    public void For_WeekWhenTheClocksGoBack_LastsAnHourLonger()
    {
        // The clocks go back at 02:00 BST on Sunday 25 October 2026, so that UK week is 169 hours long.
        var period = InsightsPeriod.For(new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc), 1);

        period.ThisWeek.Monday.Should().Be(new DateOnly(2026, 10, 19));
        period.ThisWeek.StartUtc.Should().Be(new DateTime(2026, 10, 18, 23, 0, 0, DateTimeKind.Utc));
        (period.ThisWeek.EndUtc - period.ThisWeek.StartUtc).Should().Be(TimeSpan.FromHours(169));
    }

    [Test]
    public void SameMomentLastWeek_IsAsFarIntoLastWeekAsNowIsIntoThisOne()
    {
        var period = InsightsPeriod.For(Now, 12);

        period.LastWeekStartUtc.Should().Be(LastWeekStartUtc);
        period.SameMomentLastWeekUtc.Should().Be(Now.AddDays(-7));
    }

    [Test]
    public void For_NoWeeks_Throws()
    {
        var act = () => InsightsPeriod.For(Now, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestCase("2026-06-15", "2026-06-15")]
    [TestCase("2026-06-17", "2026-06-15")]
    [TestCase("2026-06-21", "2026-06-15")]
    public void MondayOf_AnyDay_ReturnsItsWeeksMonday(string day, string monday)
    {
        InsightsPeriod.MondayOf(DateOnly.Parse(day)).Should().Be(DateOnly.Parse(monday));
    }

    [Test]
    public void Week_Contains_IncludesItsStartAndNotItsEnd()
    {
        var week = InsightsPeriod.For(Now, 1).ThisWeek;

        week.Contains(week.StartUtc).Should().BeTrue();
        week.Contains(week.EndUtc.AddTicks(-1)).Should().BeTrue();
        week.Contains(week.EndUtc).Should().BeFalse();
        week.Contains(week.StartUtc.AddTicks(-1)).Should().BeFalse();
    }
}
