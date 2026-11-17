using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Procedures.Insights.Common;
using MedApp.DevMcp.DemoData;
using static MedApp.UnitTests.Common.Constants.DemoDataTestConstants;

namespace MedApp.UnitTests.Tools.DemoData;

[TestFixture]
public sealed class VolumeCurvesTests
{
    [Test]
    public void Expected_DayCase_RampsUpFromItsLaunchAndIsZeroBefore()
    {
        var weekly = Enumerable.Range(0, VolumeCurves.DayCaseLaunchedWeeksAgo + 1)
            .Select(w => VolumeCurves.Expected(Pathway.DayCase, w)).Reverse().ToList();

        weekly.Should().BeInAscendingOrder();
        weekly[0].Should().BeApproximately(3, 0.01);
        weekly[^1].Should().BeApproximately(9, 0.01);
        VolumeCurves.Expected(Pathway.DayCase, VolumeCurves.DayCaseLaunchedWeeksAgo + 1).Should().Be(0);
    }

    [Test]
    public void Expected_ArchivedDayCase_RanUntilTheNewOneAndStopped()
    {
        VolumeCurves.Expected(Pathway.DayCase2024, VolumeCurves.DayCaseLaunchedWeeksAgo).Should().Be(0);
        VolumeCurves.Expected(Pathway.DayCase2024, 0).Should().Be(0);
        VolumeCurves.Expected(Pathway.DayCase2024, VolumeCurves.DayCaseLaunchedWeeksAgo + 1).Should().Be(5);
    }

    [Test]
    public void Expected_Diagnostic_HasASummerDipAndASpike()
    {
        var usual = VolumeCurves.Expected(Pathway.Diagnostic, 5);

        VolumeCurves.Expected(Pathway.Diagnostic, 9).Should().BeLessThan(usual);
        VolumeCurves.Expected(Pathway.Diagnostic, 2).Should().BeGreaterThan(usual * 1.5);
        VolumeCurves.Expected(Pathway.Diagnostic, 3).Should().BeGreaterThan(usual * 1.5);
    }

    [Test]
    public void Expected_Minor_HadALowWeekLastWeek()
    {
        VolumeCurves.Expected(Pathway.Minor, 1).Should().BeLessThan(VolumeCurves.Expected(Pathway.Minor, 2) / 2);
    }

    [Test]
    public void Expected_Physio_OnlySinceTheServiceStarted()
    {
        VolumeCurves.Expected(Pathway.Physio, VolumeCurves.PhysioStartedWeeksAgo).Should().BeGreaterThan(0);
        VolumeCurves.Expected(Pathway.Physio, VolumeCurves.PhysioStartedWeeksAgo + 1).Should().Be(0);
    }

    [Test]
    public void Expected_BeforeTheDetailedWeeks_IsThinner()
    {
        VolumeCurves.Expected(Pathway.Surgical, VolumeCurves.DetailedWeeks)
            .Should().BeLessThan(VolumeCurves.Expected(Pathway.Surgical, VolumeCurves.DetailedWeeks - 1));
        VolumeCurves.Expected(Pathway.Surgical, VolumeCurves.HistoryWeeks).Should().Be(0);
    }

    [TestCase(Pathway.DayCase)]
    [TestCase(Pathway.Diagnostic)]
    [TestCase(Pathway.Minor)]
    public void CreationTimes_AreWeekdaysInWorkingHours_InOrder_AndNotInTheFuture(Pathway pathway)
    {
        var times = VolumeCurves.CreationTimes(pathway, Now, new Random(Seed));

        times.Should().NotBeEmpty().And.BeInAscendingOrder();
        times.Should().OnlyContain(t => t < Now
                                        && WorkingHours.IsWorkingDay(UkTime.DateOf(t))
                                        && UkTime.TimeOf(t) >= WorkingHours.Start
                                        && UkTime.TimeOf(t) < WorkingHours.End);
    }

    [Test]
    public void CreationTimes_FollowTheCurve_WeekByWeek()
    {
        var times = VolumeCurves.CreationTimes(Pathway.Diagnostic, Now, new Random(Seed));
        var thisMonday = InsightsPeriod.MondayOf(UkTime.DateOf(Now));
        int InWeek(int weeksAgo) => times.Count(t => InsightsPeriod.MondayOf(UkTime.DateOf(t)) == thisMonday.AddDays(-7 * weeksAgo));

        InWeek(2).Should().BeInRange(11, 13);
        InWeek(9).Should().BeInRange(2, 4);
        InWeek(5).Should().BeInRange(5, 7);
    }

    [Test]
    public void CreationTimes_SameSeed_GiveTheSameTimes()
    {
        var first = VolumeCurves.CreationTimes(Pathway.Surgical, Now, new Random(Seed));
        var second = VolumeCurves.CreationTimes(Pathway.Surgical, Now, new Random(Seed));

        first.Should().Equal(second);
    }
}
