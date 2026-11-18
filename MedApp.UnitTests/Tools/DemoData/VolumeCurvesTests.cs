using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Procedures.Insights.Common;
using MedApp.DevMcp.DemoData;
using static MedApp.UnitTests.Common.Constants.DemoDataTestConstants;

namespace MedApp.UnitTests.Tools.DemoData;

[TestFixture]
public sealed class VolumeCurvesTests
{
    /// <summary>The week's expected referrals, counted back from the test's "now" (Monday 16 November 2026).</summary>
    private static double Expected(Pathway pathway, int weeksAgo) => VolumeCurves.Expected(pathway, weeksAgo, Now);

    private static double Season(Pathway pathway, int weeksAgo) => VolumeCurves.Season(pathway, Monday.AddDays(-7 * weeksAgo));

    [Test]
    public void Expected_SkinLesion_RampsUpFromItsRedesignAndIsZeroBefore()
    {
        var launch = VolumeCurves.SkinLesionRedesignedWeeksAgo;

        // Without the season (the redesign was in July, when more are referred), the ramp is 3 to 8 over six weeks.
        var ramp = Enumerable.Range(0, launch + 1).Select(w => Expected(Pathway.SkinLesion, w) / Season(Pathway.SkinLesion, w)).Reverse().ToList();
        ramp.Should().BeInAscendingOrder();
        ramp[0].Should().BeApproximately(3, 0.01);
        ramp[VolumeCurves.SkinLesionRampWeeks].Should().BeApproximately(8, 0.01);
        ramp[^1].Should().BeApproximately(8, 0.01);

        Expected(Pathway.SkinLesion, 0).Should().BeApproximately(8, 0.01, "November is neither summer nor winter");
        Expected(Pathway.SkinLesion, launch + 1).Should().Be(0);
    }

    [Test]
    public void Expected_ArchivedSkinLesion_RanUntilTheRedesignAndStopped()
    {
        var lastWeek = VolumeCurves.SkinLesionRedesignedWeeksAgo + 1;

        Expected(Pathway.SkinLesion2024, VolumeCurves.SkinLesionRedesignedWeeksAgo).Should().Be(0);
        Expected(Pathway.SkinLesion2024, 0).Should().Be(0);
        Expected(Pathway.SkinLesion2024, lastWeek).Should().BeApproximately(5 * Season(Pathway.SkinLesion2024, lastWeek), 0.01);
    }

    [Test]
    public void Season_SkinLesionsPeakInSummer_AndInflammatorySkinInWinter_AndTheRestAreFlat()
    {
        var (january, july, october) = (new DateOnly(2026, 1, 12), new DateOnly(2026, 7, 13), new DateOnly(2026, 10, 12));

        VolumeCurves.Season(Pathway.SkinLesion, july).Should().BeGreaterThan(VolumeCurves.Season(Pathway.SkinLesion, october));
        VolumeCurves.Season(Pathway.SkinLesion, october).Should().BeGreaterThan(VolumeCurves.Season(Pathway.SkinLesion, january));
        VolumeCurves.Season(Pathway.SkinLesion2024, july).Should().Be(VolumeCurves.Season(Pathway.SkinLesion, july));
        VolumeCurves.Season(Pathway.InflammatorySkin, january).Should().BeGreaterThan(VolumeCurves.Season(Pathway.InflammatorySkin, october));
        VolumeCurves.Season(Pathway.InflammatorySkin, october).Should().BeGreaterThan(VolumeCurves.Season(Pathway.InflammatorySkin, july));
        foreach (var pathway in new[] { Pathway.PostMenopausalBleeding, Pathway.Prostate, Pathway.Diabetes, Pathway.Biomechanics })
            new[] { january, july, october }.Select(m => VolumeCurves.Season(pathway, m)).Should().OnlyContain(s => s == 1);
    }

    [Test]
    public void Expected_PostMenopausalBleeding_HasADipAndASpike()
    {
        var usual = Expected(Pathway.PostMenopausalBleeding, 5);

        Expected(Pathway.PostMenopausalBleeding, 9).Should().BeLessThan(usual);
        Expected(Pathway.PostMenopausalBleeding, 2).Should().BeGreaterThan(usual * 1.5);
        Expected(Pathway.PostMenopausalBleeding, 3).Should().BeGreaterThan(usual * 1.5);
    }

    [Test]
    public void Expected_InflammatorySkin_HadALowWeekLastWeek()
    {
        Expected(Pathway.InflammatorySkin, 1).Should().BeLessThan(Expected(Pathway.InflammatorySkin, 2) / 2);
    }

    /// <summary>The week of Monday 26 October 2026 has 1 November in it; the week of 2 November has no 1st.</summary>
    [Test]
    public void Expected_Diabetes_JumpsInTheWeekTheRecallLettersGoOut()
    {
        VolumeCurves.IsRecallWeek(new DateOnly(2026, 10, 26)).Should().BeTrue();
        VolumeCurves.IsRecallWeek(new DateOnly(2026, 11, 2)).Should().BeFalse();
        VolumeCurves.IsRecallWeek(new DateOnly(2026, 6, 1)).Should().BeTrue("the 1st is that Monday");

        Expected(Pathway.Diabetes, 3).Should().BeGreaterThan(Expected(Pathway.Diabetes, 2) * 2, "three weeks before 16 November is the week of 26 October");
    }

    [Test]
    public void Expected_Biomechanics_OnlySinceTheServiceStarted()
    {
        Expected(Pathway.Biomechanics, VolumeCurves.BiomechanicsStartedWeeksAgo).Should().BeGreaterThan(0);
        Expected(Pathway.Biomechanics, VolumeCurves.BiomechanicsStartedWeeksAgo + 1).Should().Be(0);
    }

    [Test]
    public void Expected_BeforeTheDetailedWeeks_IsThinner()
    {
        Expected(Pathway.Prostate, VolumeCurves.DetailedWeeks)
            .Should().BeLessThan(Expected(Pathway.Prostate, VolumeCurves.DetailedWeeks - 1));
        Expected(Pathway.Prostate, VolumeCurves.HistoryWeeks).Should().Be(0);
    }

    [TestCase(Pathway.SkinLesion)]
    [TestCase(Pathway.PostMenopausalBleeding)]
    [TestCase(Pathway.Diabetes)]
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
        var times = VolumeCurves.CreationTimes(Pathway.SkinLesion, Now, new Random(Seed));
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
        var first = VolumeCurves.CreationTimes(Pathway.Prostate, Now, new Random(Seed));
        var second = VolumeCurves.CreationTimes(Pathway.Prostate, Now, new Random(Seed));

        first.Should().Equal(second);
    }
}
