using FluentAssertions;
using MedApp.DevMcp.DemoData;
using NUnit.Framework;

namespace MedApp.UnitTests.Tools.DemoData;

[TestFixture]
public sealed class DemoTimelineTests
{
    [TestCase("2026-11-18T10:00:00Z", false)]
    [TestCase("2026-11-20T21:00:00Z", false)]
    [TestCase("2026-11-21T10:00:00Z", true)]
    [TestCase("2026-11-22T23:30:00Z", true)]
    [TestCase("2026-11-23T00:30:00Z", false)]
    [TestCase("2026-11-22T10:00:00Z", true)]
    public void IsWeekend_ByUkDate_SaysWhetherSeededAtTheWeekend(string nowUtc, bool expected)
    {
        var timeline = new DemoTimeline(DateTime.Parse(nowUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal));

        timeline.IsWeekend.Should().Be(expected);
    }
}
