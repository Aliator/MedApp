using FluentAssertions;
using MedApp.Application.Lab.Common;
using MedApp.Domain.Lab;
using static MedApp.UnitTests.Common.Constants.SpecimensTestConstants;

namespace MedApp.UnitTests.Application.TheLab.Common;

[TestFixture]
public sealed class LabClockTests
{
    // 10:00 UK time (BST) on 1 June 2026.
    private static readonly DateTime Received = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    private static LabClockInput Input(DateTime? reportedAtUtc = null, LabCaseStatus status = LabCaseStatus.Open, LabClockHospital? hospital = null) =>
        new(Received, status, reportedAtUtc, Histology(), hospital);

    [Test]
    public void NotReceived_HasNoClock() =>
        LabClock.Of(new LabClockInput(null, LabCaseStatus.Open, null, Histology(), null), Received).Should().BeNull();

    [Test]
    public void ClinicCase_IsReceivedPlusTheTestsDays_InUkDates()
    {
        var clock = LabClock.Of(Input(), Received)!;

        clock.LabDueOn.Should().Be(new DateOnly(2026, 6, 8));
        clock.LabLimitOn.Should().Be(new DateOnly(2026, 6, 11));
        clock.DueOn.Should().Be(new DateOnly(2026, 6, 8));
        clock.SetBy.Should().Be("Lab");
        clock.AtRiskOn.Should().Be(new DateOnly(2026, 6, 6));
        clock.HospitalDueOn.Should().BeNull();
        clock.State.Should().Be(LabClock.OnTime);
        clock.DaysLeft.Should().Be(7);
        // The end of 8 June, UK time (BST), in UTC.
        clock.DueAtUtc.Should().BeCloseTo(new DateTime(2026, 6, 8, 23, 0, 0, DateTimeKind.Utc), TimeSpan.FromSeconds(1));
    }

    [Test]
    public void State_FollowsTheDates_AndTheReport()
    {
        LabClock.Of(Input(), new DateTime(2026, 6, 5, 22, 0, 0, DateTimeKind.Utc))!.State.Should().Be(LabClock.OnTime);
        // From the start of 6 June, UK time.
        LabClock.Of(Input(), new DateTime(2026, 6, 5, 23, 30, 0, DateTimeKind.Utc))!.State.Should().Be(LabClock.AtRisk);
        LabClock.Of(Input(), new DateTime(2026, 6, 8, 22, 0, 0, DateTimeKind.Utc))!.State.Should().Be(LabClock.AtRisk);
        LabClock.Of(Input(), new DateTime(2026, 6, 9, 8, 0, 0, DateTimeKind.Utc))!.State.Should().Be(LabClock.Breached);
        LabClock.Of(Input(reportedAtUtc: new DateTime(2026, 6, 8, 9, 0, 0, DateTimeKind.Utc)), Received.AddDays(20))!.State
            .Should().Be(LabClock.ReportedOnTime);
        LabClock.Of(Input(reportedAtUtc: new DateTime(2026, 6, 9, 9, 0, 0, DateTimeKind.Utc)), Received.AddDays(20))!.State
            .Should().Be(LabClock.ReportedLate);
        LabClock.Of(Input(status: LabCaseStatus.RepeatRequested), Received.AddDays(20))!.State.Should().Be(LabClock.Stopped);
    }

    [Test]
    public void ATestWithNone_UsesTheRCPathDefaults()
    {
        var clock = LabClock.Of(new LabClockInput(Received, LabCaseStatus.Open, null, null, null), Received)!;

        clock.LabDueOn.Should().Be(new DateOnly(2026, 6, 8));
        clock.LabLimitOn.Should().Be(new DateOnly(2026, 6, 11));
        clock.AtRiskOn.Should().Be(new DateOnly(2026, 6, 6));
    }

    [Test]
    public void HospitalCase_TheSoonerApplies_ATieNamingTheHospital()
    {
        var needed = LabClock.Of(Input(hospital: new LabClockHospital(new DateOnly(2026, 6, 4), false, null, 10)), Received)!;
        needed.Should().BeEquivalentTo(new { HospitalDueBasis = "ReportNeededBy", DueOn = new DateOnly(2026, 6, 4), SetBy = "Hospital" });

        var tie = LabClock.Of(Input(hospital: new LabClockHospital(new DateOnly(2026, 6, 8), false, null, 10)), Received)!;
        tie.SetBy.Should().Be("Hospital");

        var later = LabClock.Of(Input(hospital: new LabClockHospital(null, false, null, 10)), Received)!;
        later.Should().BeEquivalentTo(new { HospitalDueBasis = "HospitalDefault", HospitalDueOn = new DateOnly(2026, 6, 11), SetBy = "Lab" });

        var cancer = LabClock.Of(Input(hospital: new LabClockHospital(null, true, Received.AddDays(-5), 10)), Received)!;
        cancer.Should().BeEquivalentTo(new { HospitalDueBasis = "CancerPathway", HospitalDueOn = new DateOnly(2026, 6, 6), SetBy = "Hospital" });
    }
}
