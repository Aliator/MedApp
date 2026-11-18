using MedApp.Application.Calendar.Common;
using MedApp.Application.Pathways.Insights.Common;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// The moments in the centre's story, all relative to "now", so the data always looks current: when the app started
/// being used, when people joined and left, when the skin lesion pathway was redesigned, and the windows notifications
/// and the activity log cover.
/// </summary>
public sealed class DemoTimeline
{
    public DemoTimeline(DateTime nowUtc)
    {
        NowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        Today = UkTime.DateOf(NowUtc);
        ThisMonday = InsightsPeriod.MondayOf(Today);

        // Today's diary is today's; on a weekend or a bank holiday (when nobody's booked) it's the next working day.
        var diaryDay = Today;
        while (!DemoClosedDays.IsWorkingDay(diaryDay))
            diaryDay = diaryDay.AddDays(1);
        DiaryDay = diaryDay;

        AppStartUtc = At(ThisMonday.AddDays(-7 * VolumeCurves.HistoryWeeks), 9, 0);
        SkinLesionRedesignedUtc = At(ThisMonday.AddDays(-7 * VolumeCurves.SkinLesionRedesignedWeeksAgo), 8, 30);
        BiomechanicsStartedUtc = At(ThisMonday.AddDays(-7 * VolumeCurves.BiomechanicsStartedWeeksAgo), 8, 30);
        LeaverLeftUtc = At(ThisMonday.AddDays(-7 * 16 + 4), 17, 0);
        ManagerMadeAdminUtc = WorkingMoment(NowUtc.AddDays(-49), 10, 15);
        TwoFactorEnabledUtc = WorkingMoment(NowUtc.AddDays(-42), 9, 40);
        PasswordResetUtc = WorkingMoment(NowUtc.AddDays(-21), 8, 50);
        NotificationsTurnedOffUtc = WorkingMoment(NowUtc.AddDays(-45), 16, 20);
        MonitoringRulesUtc = WorkingMoment(NowUtc.AddDays(-63), 11, 0);
        LocumRoleCreatedUtc = WorkingMoment(NowUtc.AddDays(-56), 11, 30);
        RolesRefinedUtc = WorkingMoment(NowUtc.AddDays(-28), 14, 0);
        NewServiceAddedUtc = WorkingMoment(NowUtc.AddDays(-24), 10, 30);
        LabAndLettersFromUtc = WorkingMoment(NowUtc.AddDays(-70), 8, 30);
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

    /// <summary>When the centre started using MedApp: about 18 months ago. Older records were imported.</summary>
    public DateTime AppStartUtc { get; }

    /// <summary>When the redesigned skin lesion pathway replaced the 2024 one, which was archived then.</summary>
    public DateTime SkinLesionRedesignedUtc { get; }

    /// <summary>When the biomechanics service started; its podiatrist joined the week before.</summary>
    public DateTime BiomechanicsStartedUtc { get; }

    /// <summary>The leaver's last day, when their account was deleted.</summary>
    public DateTime LeaverLeftUtc { get; }

    public DateTime ManagerMadeAdminUtc { get; }

    public DateTime TwoFactorEnabledUtc { get; }

    public DateTime PasswordResetUtc { get; }

    public DateTime NotificationsTurnedOffUtc { get; }

    public DateTime MonitoringRulesUtc { get; }

    /// <summary>The lead made a role for locums covering clinics (archived at <see cref="RolesRefinedUtc"/>).</summary>
    public DateTime LocumRoleCreatedUtc { get; }

    /// <summary>The lead gave the visiting consultants a role of their own, and archived the locums' role.</summary>
    public DateTime RolesRefinedUtc { get; }

    public DateTime NewStarterCreatedUtc { get; }

    /// <summary>When the lead added the new service (Respiratory), whose referrals nobody can triage yet.</summary>
    public DateTime NewServiceAddedUtc { get; }

    /// <summary>
    /// When the centre started tracking specimens, results, consent, letters and the MDT list in MedApp (Phases 3 to 5),
    /// ten weeks ago: specimens are taken on stages started from then, and pathways created from then carry the stage
    /// rules that came with them (a lab stage waits for results, the biopsy needs consent, the discharge letter stage
    /// needs a discharge summary). Older records were done on paper, with reports attached as files.
    /// </summary>
    public DateTime LabAndLettersFromUtc { get; }

    /// <summary>Notifications are made for what happened from here on (30 days).</summary>
    public DateTime NotificationsFromUtc { get; }

    /// <summary>The activity log's sign-ins, views and searches start here (60 days).</summary>
    public DateTime ActivityFromUtc { get; }

    /// <summary>When an account was made: everyone a few weeks before the centre started, except later joiners.</summary>
    public DateTime AccountCreatedUtc(StaffKind kind) => kind switch
    {
        StaffKind.NewStarter => NewStarterCreatedUtc,
        StaffKind.Podiatrist => BiomechanicsStartedUtc.AddDays(-7),
        StaffKind.Leaver => AppStartUtc.AddDays(-25),
        _ => AppStartUtc.AddDays(-21).AddMinutes(10 * (int)kind),
    };

    /// <summary>A UK clock time on a UK date, in UTC.</summary>
    public static DateTime At(DateOnly date, int hour, int minute) => UkTime.ToUtc(date, new TimeOnly(hour, minute));

    /// <summary>
    /// The weekday on or after a moment's date (UK), at a clock time: when someone did something. Bank holidays count,
    /// as nothing stops someone signing in then, and moving "three days ago" past one could put it after now; only what
    /// goes on a calendar keeps off them (<see cref="DemoClosedDays"/>).
    /// </summary>
    public static DateTime WorkingMoment(DateTime utc, int hour, int minute)
    {
        var date = UkTime.DateOf(utc);
        while (!WorkingHours.IsWorkingDay(date))
            date = date.AddDays(1);
        return At(date, hour, minute);
    }

    /// <summary>
    /// The moment, or the next one in working hours (08:00 to 18:00 UK time, Monday to Friday) if it's outside them:
    /// staff do things in the working day. Bank holidays count, as for <see cref="WorkingMoment"/>.
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
