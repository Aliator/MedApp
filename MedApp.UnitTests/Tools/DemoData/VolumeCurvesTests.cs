using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Pathways.Insights.Common;
using MedApp.DevMcp.DemoData;
using static MedApp.UnitTests.Common.Constants.DemoDataTestConstants;

namespace MedApp.UnitTests.Tools.DemoData;

[TestFixture]
public sealed class VolumeCurvesTests
{
    /// <summary>The week's expected referrals, counted back from the test's "now" (Monday 16 November 2026).</summary>
    private static double Expected(PathwayKind pathwayKind, int weeksAgo) => VolumeCurves.Expected(pathwayKind, weeksAgo, Now);

    private static double Season(PathwayKind pathwayKind, int weeksAgo) => VolumeCurves.Season(pathwayKind, Monday.AddDays(-7 * weeksAgo));

    [Test]
    public void Expected_SkinLesion_RampsUpFromItsRedesignAndIsZeroBefore()
    {
        var launch = VolumeCurves.SkinLesionRedesignedWeeksAgo;

        // Without the season (the redesign was in July, when more are referred), the ramp is 3 to 8 over six weeks.
        var ramp = Enumerable.Range(0, launch + 1).Select(w => Expected(PathwayKind.SkinLesion, w) / Season(PathwayKind.SkinLesion, w)).Reverse().ToList();
        ramp.Should().BeInAscendingOrder();
        ramp[0].Should().BeApproximately(3, 0.01);
        ramp[VolumeCurves.SkinLesionRampWeeks].Should().BeApproximately(8, 0.01);
        ramp[^1].Should().BeApproximately(8, 0.01);

        Expected(PathwayKind.SkinLesion, 0).Should().BeApproximately(8, 0.01, "November is neither summer nor winter");
        Expected(PathwayKind.SkinLesion, launch + 1).Should().Be(0);
    }

    [Test]
    public void Expected_ArchivedSkinLesion_RanUntilTheRedesignAndStopped()
    {
        var lastWeek = VolumeCurves.SkinLesionRedesignedWeeksAgo + 1;

        Expected(PathwayKind.SkinLesion2024, VolumeCurves.SkinLesionRedesignedWeeksAgo).Should().Be(0);
        Expected(PathwayKind.SkinLesion2024, 0).Should().Be(0);
        Expected(PathwayKind.SkinLesion2024, lastWeek).Should().BeApproximately(5 * Season(PathwayKind.SkinLesion2024, lastWeek), 0.01);
    }

    [Test]
    public void Season_SkinLesionsPeakInSummer_AndInflammatorySkinInWinter_AndTheRestAreFlat()
    {
        var (january, july, october) = (new DateOnly(2026, 1, 12), new DateOnly(2026, 7, 13), new DateOnly(2026, 10, 12));

        VolumeCurves.Season(PathwayKind.SkinLesion, july).Should().BeGreaterThan(VolumeCurves.Season(PathwayKind.SkinLesion, october));
        VolumeCurves.Season(PathwayKind.SkinLesion, october).Should().BeGreaterThan(VolumeCurves.Season(PathwayKind.SkinLesion, january));
        VolumeCurves.Season(PathwayKind.SkinLesion2024, july).Should().Be(VolumeCurves.Season(PathwayKind.SkinLesion, july));
        VolumeCurves.Season(PathwayKind.InflammatorySkin, january).Should().BeGreaterThan(VolumeCurves.Season(PathwayKind.InflammatorySkin, october));
        VolumeCurves.Season(PathwayKind.InflammatorySkin, october).Should().BeGreaterThan(VolumeCurves.Season(PathwayKind.InflammatorySkin, july));
        foreach (var pathwayKind in new[] { PathwayKind.PostMenopausalBleeding, PathwayKind.Prostate, PathwayKind.Diabetes, PathwayKind.Biomechanics })
            new[] { january, july, october }.Select(m => VolumeCurves.Season(pathwayKind, m)).Should().OnlyContain(s => s == 1);
    }

    [Test]
    public void Expected_PostMenopausalBleeding_HasADipAndASpike()
    {
        var usual = Expected(PathwayKind.PostMenopausalBleeding, 5);

        Expected(PathwayKind.PostMenopausalBleeding, 9).Should().BeLessThan(usual);
        Expected(PathwayKind.PostMenopausalBleeding, 2).Should().BeGreaterThan(usual * 1.5);
        Expected(PathwayKind.PostMenopausalBleeding, 3).Should().BeGreaterThan(usual * 1.5);
    }

    [Test]
    public void Expected_InflammatorySkin_HadALowWeekLastWeek()
    {
        Expected(PathwayKind.InflammatorySkin, 1).Should().BeLessThan(Expected(PathwayKind.InflammatorySkin, 2) / 2);
    }

    /// <summary>The week of Monday 26 October 2026 has 1 November in it; the week of 2 November has no 1st.</summary>
    [Test]
    public void Expected_Diabetes_JumpsInTheWeekTheRecallLettersGoOut()
    {
        VolumeCurves.IsRecallWeek(new DateOnly(2026, 10, 26)).Should().BeTrue();
        VolumeCurves.IsRecallWeek(new DateOnly(2026, 11, 2)).Should().BeFalse();
        VolumeCurves.IsRecallWeek(new DateOnly(2026, 6, 1)).Should().BeTrue("the 1st is that Monday");

        Expected(PathwayKind.Diabetes, 3).Should().BeGreaterThan(Expected(PathwayKind.Diabetes, 2) * 2, "three weeks before 16 November is the week of 26 October");
    }

    [Test]
    public void Expected_Biomechanics_OnlySinceTheServiceStarted()
    {
        Expected(PathwayKind.Biomechanics, VolumeCurves.BiomechanicsStartedWeeksAgo).Should().BeGreaterThan(0);
        Expected(PathwayKind.Biomechanics, VolumeCurves.BiomechanicsStartedWeeksAgo + 1).Should().Be(0);
    }

    [Test]
    public void Expected_BeforeTheDetailedWeeks_IsThinner()
    {
        Expected(PathwayKind.Prostate, VolumeCurves.DetailedWeeks)
            .Should().BeLessThan(Expected(PathwayKind.Prostate, VolumeCurves.DetailedWeeks - 1));
        Expected(PathwayKind.Prostate, VolumeCurves.HistoryWeeks).Should().Be(0);
    }

    [TestCase(PathwayKind.SkinLesion)]
    [TestCase(PathwayKind.PostMenopausalBleeding)]
    [TestCase(PathwayKind.Diabetes)]
    public void CreationTimes_AreWeekdaysInWorkingHours_InOrder_AndNotInTheFuture(PathwayKind pathwayKind)
    {
        var times = VolumeCurves.CreationTimes(pathwayKind, Now, new Random(Seed));

        times.Should().NotBeEmpty().And.BeInAscendingOrder();
        times.Should().OnlyContain(t => t < Now
                                        && WorkingHours.IsWorkingDay(UkTime.DateOf(t))
                                        && UkTime.TimeOf(t) >= WorkingHours.Start
                                        && UkTime.TimeOf(t) < WorkingHours.End);
    }

    [Test]
    public void CreationTimes_FollowTheCurve_WeekByWeek()
    {
        var times = VolumeCurves.CreationTimes(PathwayKind.SkinLesion, Now, new Random(Seed));
        var thisMonday = InsightsPeriod.MondayOf(UkTime.DateOf(Now));
        int InWeek(int weeksAgo) => times.Count(t => InsightsPeriod.MondayOf(UkTime.DateOf(t)) == thisMonday.AddDays(-7 * weeksAgo));

        InWeek(2).Should().Be(8);
        InWeek(5).Should().Be(8);
        InWeek(VolumeCurves.SkinLesionRedesignedWeeksAgo).Should().BeInRange(3, 4, "3.75 in its first week, in July");
        InWeek(VolumeCurves.SkinLesionRedesignedWeeksAgo + 1).Should().Be(0);
    }

    [Test]
    public void CreationTimes_SameSeed_GiveTheSameTimes()
    {
        var first = VolumeCurves.CreationTimes(PathwayKind.Prostate, Now, new Random(Seed));
        var second = VolumeCurves.CreationTimes(PathwayKind.Prostate, Now, new Random(Seed));

        first.Should().Equal(second);
    }
}
