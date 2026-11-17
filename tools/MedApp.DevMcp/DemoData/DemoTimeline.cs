using MedApp.Application.Calendar.Common;
using MedApp.Application.Procedures.Insights.Common;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// The moments in the clinic's story, all relative to "now", so the data always looks current: when the app started
/// being used, when people joined and left, when the day-case pathway was replaced, and the windows notifications
/// and the activity log cover.
/// </summary>
public sealed class DemoTimeline
{
    public DemoTimeline(DateTime nowUtc)
    {
        NowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        Today = UkTime.DateOf(NowUtc);
        ThisMonday = InsightsPeriod.MondayOf(Today);

        // Today's diary is today's; on a weekend (when nobody's booked) it's the next working day.
        DiaryDay = WorkingHours.IsWorkingDay(Today) ? Today : Today.AddDays(Today.DayOfWeek == DayOfWeek.Saturday ? 2 : 1);

        AppStartUtc = At(ThisMonday.AddDays(-7 * VolumeCurves.HistoryWeeks), 9, 0);
        DayCaseLaunchedUtc = At(ThisMonday.AddDays(-7 * VolumeCurves.DayCaseLaunchedWeeksAgo), 8, 30);
        PhysioStartedUtc = At(ThisMonday.AddDays(-7 * VolumeCurves.PhysioStartedWeeksAgo), 8, 30);
        LeaverLeftUtc = At(ThisMonday.AddDays(-7 * 16 + 4), 17, 0);
        ManagerMadeAdminUtc = WorkingMoment(NowUtc.AddDays(-49), 10, 15);
        TwoFactorEnabledUtc = WorkingMoment(NowUtc.AddDays(-42), 9, 40);
        PasswordResetUtc = WorkingMoment(NowUtc.AddDays(-21), 8, 50);
        NotificationsTurnedOffUtc = WorkingMoment(NowUtc.AddDays(-45), 16, 20);
        MonitoringRulesUtc = WorkingMoment(NowUtc.AddDays(-63), 11, 0);
        NotificationsFromUtc = NowUtc.AddDays(-30);
        ActivityFromUtc = NowUtc.AddDays(-60);

        var thisWeek = At(ThisMonday, 9, 30);
        NewStarterCreatedUtc = thisWeek < NowUtc.AddHours(-1) ? thisWeek : WorkingMoment(NowUtc.AddDays(-3), 9, 30);
    }

    public DateTime NowUtc { get; }

    /// <summary>Today, UK time.</summary>
    public DateOnly Today { get; }

    public DateOnly ThisMonday { get; }

    /// <summary>The day whose diary the demo fills: today, or Monday when today is a weekend.</summary>
    public DateOnly DiaryDay { get; }

    /// <summary>When the clinic started using MedApp: about 18 months ago. Older records were imported.</summary>
    public DateTime AppStartUtc { get; }

    /// <summary>When the new day-case pathway replaced the 2024 one, which was archived then.</summary>
    public DateTime DayCaseLaunchedUtc { get; }

    public DateTime PhysioStartedUtc { get; }

    /// <summary>The leaver's last day, when their account was deleted.</summary>
    public DateTime LeaverLeftUtc { get; }

    public DateTime ManagerMadeAdminUtc { get; }

    public DateTime TwoFactorEnabledUtc { get; }

    public DateTime PasswordResetUtc { get; }

    public DateTime NotificationsTurnedOffUtc { get; }

    public DateTime MonitoringRulesUtc { get; }

    public DateTime NewStarterCreatedUtc { get; }

    /// <summary>Notifications are made for what happened from here on (30 days).</summary>
    public DateTime NotificationsFromUtc { get; }

    /// <summary>The activity log's sign-ins, views and searches start here (60 days).</summary>
    public DateTime ActivityFromUtc { get; }

    /// <summary>When an account was made: everyone a few weeks before the clinic started, except later joiners.</summary>
    public DateTime AccountCreatedUtc(StaffKind kind) => kind switch
    {
        StaffKind.NewStarter => NewStarterCreatedUtc,
        StaffKind.Physiotherapist => PhysioStartedUtc.AddDays(-7),
        StaffKind.Leaver => AppStartUtc.AddDays(-25),
        _ => AppStartUtc.AddDays(-21).AddMinutes(10 * (int)kind),
    };

    /// <summary>A UK clock time on a UK date, in UTC.</summary>
    public static DateTime At(DateOnly date, int hour, int minute) => UkTime.ToUtc(date, new TimeOnly(hour, minute));

    /// <summary>The working day on or after a moment's date (UK), at a clock time.</summary>
    public static DateTime WorkingMoment(DateTime utc, int hour, int minute)
    {
        var date = UkTime.DateOf(utc);
        while (!WorkingHours.IsWorkingDay(date))
            date = date.AddDays(1);
        return At(date, hour, minute);
    }

    /// <summary>
    /// The moment, or the next one in working hours (08:00 to 18:00 UK time, Monday to Friday) if it's outside them:
    /// staff do things in the working day.
    /// </summary>
    public static DateTime InWorkingHours(DateTime utc, Random random)
    {
        var date = UkTime.DateOf(utc);
        var time = UkTime.TimeOf(utc);
        if (WorkingHours.IsWorkingDay(date) && time >= WorkingHours.Start && time < WorkingHours.End)
            return utc;

        if (time >= WorkingHours.End || !WorkingHours.IsWorkingDay(date))
            date = date.AddDays(1);
        while (!WorkingHours.IsWorkingDay(date))
            date = date.AddDays(1);

        return At(date, 8, 0).AddMinutes(random.Next(5, 150));
    }
}
