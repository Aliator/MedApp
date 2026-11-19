using MedApp.Application.Calendar.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Lab.Responses;
using MedApp.Domain.Lab;

namespace MedApp.Application.Lab.Common;

/// <summary>The hospital's side of a hospital case's clock (MC18 and MC19), from its request and hospital.</summary>
public sealed record LabClockHospital(DateOnly? ReportNeededBy, bool IsCancerPathway, DateTime? CollectedAtUtc, int DefaultDeadlineDays);

/// <summary>What a lab case's clock is worked out from: its receipt, status, report, test and (a hospital case's) hospital.</summary>
public sealed record LabClockInput(
    DateTime? ReceivedAtUtc,
    LabCaseStatus Status,
    DateTime? ReportedAtUtc,
    LabTest? Test,
    LabClockHospital? Hospital);

/// <summary>
/// A lab case's strict clock (MC19 of <c>docs/multi-clinic-plan.md</c>, decisions 186 and Q15; the light demo's
/// <c>labClock.ts</c>). Date arithmetic only, in calendar days and UK dates; never anything from a result.
/// <list type="bullet">
/// <item>The lab's own: received plus the test's <see cref="LabTest.TargetDays"/> (7 by default); its limit shown beside it.</item>
/// <item>The hospital's (a hospital case only): the request's date, else for a cancer pathway collection plus 10 days,
/// else received plus the hospital's default days.</item>
/// <item>Whichever is sooner applies (on a tie, the hospital's is named). Due at the end of that UK day; at risk from the
/// start of the test's at-risk days before it; breached after it until the report is authorised. A second opinion
/// keeps it running: nothing pauses it.</item>
/// </list>
/// </summary>
public static class LabClock
{
    /// <summary>NHS England's standard for a cancer pathway request that gives no date: collection plus 10 days.</summary>
    public const int CancerPathwayDays = 10;

    public const string OnTime = "OnTime";
    public const string AtRisk = "AtRisk";
    public const string Breached = "Breached";
    public const string ReportedOnTime = "ReportedOnTime";
    public const string ReportedLate = "ReportedLate";
    public const string Stopped = "Stopped";

    /// <summary>The hospital's default deadline when a case's hospital isn't loaded (as the light demo's).</summary>
    public const int FallbackHospitalDeadlineDays = 10;

    /// <summary>
    /// The case's clock, or null before the lab has received it. A hospital case's side comes from its request and
    /// hospital (read with it) unless <paramref name="hospital"/> is given.
    /// </summary>
    public static LabClockResponse? Of(LabCase labCase, DateTime nowUtc, LabClockHospital? hospital = null) =>
        Of(new LabClockInput(labCase.ReceivedAtUtc, labCase.Status, labCase.ReportedAtUtc, labCase.LabTest,
            hospital ?? HospitalOf(labCase)), nowUtc);

    /// <summary>A hospital case's side of the clock from its request (MC18), or null for a clinic case.</summary>
    public static LabClockHospital? HospitalOf(LabCase labCase) =>
        labCase.HospitalRequest is { } request
            ? new LabClockHospital(
                request.ReportNeededBy,
                request.IsCancerPathway,
                request.CollectedAtUtc,
                request.Hospital?.DefaultDeadlineDays ?? labCase.Hospital?.DefaultDeadlineDays ?? FallbackHospitalDeadlineDays)
            : null;

    /// <summary>The clock, or null before the lab has received the case.</summary>
    public static LabClockResponse? Of(LabClockInput input, DateTime nowUtc)
    {
        if (input.ReceivedAtUtc is not { } receivedAtUtc)
            return null;

        var receivedOn = UkTime.DateOf(receivedAtUtc);
        var labDueOn = receivedOn.AddDays(input.Test?.TargetDays ?? LabTest.DefaultTargetDays);
        var labLimitOn = receivedOn.AddDays(input.Test?.LimitDays ?? LabTest.DefaultLimitDays);
        var hospital = HospitalDue(input, receivedOn);
        var hospitalSets = hospital is { } h && h.On <= labDueOn;
        var dueOn = hospitalSets ? hospital!.Value.On : labDueOn;
        var atRiskOn = dueOn.AddDays(-(input.Test?.AtRiskDays ?? LabTest.DefaultAtRiskDays));
        var dueAtUtc = DueDates.EndOfDayUtc(dueOn);

        string state;
        if (input.ReportedAtUtc is { } reportedAtUtc)
            state = UkTime.DateOf(reportedAtUtc) <= dueOn ? ReportedOnTime : ReportedLate;
        else if (input.Status != LabCaseStatus.Open)
            state = Stopped;
        else if (nowUtc > dueAtUtc)
            state = Breached;
        else if (nowUtc >= DueDates.StartOfDayUtc(atRiskOn))
            state = AtRisk;
        else
            state = OnTime;

        return new LabClockResponse
        {
            LabDueOn = labDueOn,
            LabLimitOn = labLimitOn,
            HospitalDueOn = hospital?.On,
            HospitalDueBasis = hospital?.Basis,
            DueOn = dueOn,
            SetBy = hospitalSets ? "Hospital" : "Lab",
            DueAtUtc = dueAtUtc,
            AtRiskOn = atRiskOn,
            State = state,
            DaysLeft = dueOn.DayNumber - UkTime.DateOf(nowUtc).DayNumber,
        };
    }

    /// <summary>Whether a clock is still running (open, not yet reported).</summary>
    public static bool IsRunning(LabClockResponse? clock) => clock?.State is OnTime or AtRisk or Breached;

    private static (DateOnly On, string Basis)? HospitalDue(LabClockInput input, DateOnly receivedOn)
    {
        if (input.Hospital is not { } h)
            return null;
        if (h.ReportNeededBy is { } neededBy)
            return (neededBy, "ReportNeededBy");
        if (h.IsCancerPathway)
            return (UkTime.DateOf(h.CollectedAtUtc ?? input.ReceivedAtUtc!.Value).AddDays(CancerPathwayDays), "CancerPathway");
        return (receivedOn.AddDays(h.DefaultDeadlineDays), "HospitalDefault");
    }
}
