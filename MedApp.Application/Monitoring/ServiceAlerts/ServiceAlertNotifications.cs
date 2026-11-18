using System.Globalization;
using MedApp.Application.Monitoring.Common;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Notifications;

namespace MedApp.Application.Monitoring.ServiceAlerts;

/// <summary>
/// A service alert's notification (decision 135): one per rule and run, e.g. "3 Dermatology referrals have waited more
/// than 2 days for triage." A fixed title; the message holds a count, days, and names admins chose (the specialty, and
/// for results the responsible clinician's username). Never a patient, a clinical value or free text. Its
/// <see cref="Notification.SubjectId"/> is the rule's specialty, so the web app opens the list filtered to it.
/// </summary>
public static class ServiceAlertNotifications
{
    public static Notification Create(
        MonitoringRule rule, int count, string? specialtyName, string? responsibleUsername, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        UserId = rule.OwnerUserId,
        Kind = KindOf(rule.Event),
        Title = TitleOf(rule.Event),
        Message = MessageOf(rule.Event, count, rule.ThresholdDays ?? 0, specialtyName, responsibleUsername),
        SubjectId = rule.SpecialtyId,
        CreatedAtUtc = nowUtc
    };

    public static NotificationKind KindOf(MonitoringEvent monitoringEvent) => monitoringEvent switch
    {
        MonitoringEvent.ReferralNotTriaged => NotificationKind.MonitoredReferralNotTriaged,
        MonitoringEvent.ResultNotSignedOff => NotificationKind.MonitoredResultNotSignedOff,
        MonitoringEvent.VisitNotBooked => NotificationKind.MonitoredVisitNotBooked,
        MonitoringEvent.SpecimenOverdue => NotificationKind.MonitoredSpecimenOverdue,
        MonitoringEvent.WaitBreached => NotificationKind.MonitoredWaitBreached,
        MonitoringEvent.PauseReviewPassed => NotificationKind.MonitoredPauseReviewPassed,
        _ => throw new ArgumentOutOfRangeException(nameof(monitoringEvent), monitoringEvent, "Not a service alert.")
    };

    /// <summary>A fixed title for each alert.</summary>
    public static string TitleOf(MonitoringEvent monitoringEvent) => monitoringEvent switch
    {
        MonitoringEvent.ReferralNotTriaged => "Referrals awaiting triage",
        MonitoringEvent.ResultNotSignedOff => "Results awaiting sign-off",
        MonitoringEvent.VisitNotBooked => "Visits not booked",
        MonitoringEvent.SpecimenOverdue => "Specimens overdue",
        MonitoringEvent.WaitBreached => "Waiting-time targets",
        MonitoringEvent.PauseReviewPassed => "Pause reviews passed",
        _ => throw new ArgumentOutOfRangeException(nameof(monitoringEvent), monitoringEvent, "Not a service alert.")
    };

    /// <summary>What the alert found, e.g. "3 Dermatology referrals have waited more than 2 days for triage."</summary>
    public static string MessageOf(MonitoringEvent monitoringEvent, int count, int days, string? specialtyName, string? responsibleUsername)
    {
        var plural = count != 1;
        var specialty = string.IsNullOrWhiteSpace(specialtyName) ? string.Empty : $"{specialtyName} ";
        var have = plural ? "have" : "has";
        var are = plural ? "are" : "is";
        string Things(string one, string many) => $"{count} {specialty}{(plural ? many : one)}";
        var moreThan = $"more than {Days(days)}";

        var message = monitoringEvent switch
        {
            MonitoringEvent.ReferralNotTriaged => days == 0
                ? $"{Things("referral", "referrals")} received before today {are} awaiting triage."
                : $"{Things("referral", "referrals")} {have} waited {moreThan} for triage.",
            MonitoringEvent.ResultNotSignedOff =>
                $"{Things("result", "results")}{(string.IsNullOrEmpty(responsibleUsername) ? string.Empty : $" for {responsibleUsername}")} "
                + (days == 0 ? $"{are} awaiting sign-off." : $"{have} awaited sign-off for {moreThan}."),
            MonitoringEvent.VisitNotBooked => days == 0
                ? $"{Things("visit", "visits")} {are} waiting to be booked."
                : $"{Things("visit", "visits")} {have} waited {moreThan} to be booked.",
            MonitoringEvent.SpecimenOverdue => days == 0
                ? $"{Things("specimen", "specimens")} {are} past {(plural ? "their" : "its")} expected turnaround."
                : $"{Things("specimen", "specimens")} {are} {moreThan} past {(plural ? "their" : "its")} expected turnaround.",
            MonitoringEvent.WaitBreached => days == 0
                ? $"{Things("referral", "referrals")} {have} passed {(plural ? "their" : "its")} waiting-time target."
                : $"{Things("referral", "referrals")} {are} within {Days(days)} of {(plural ? "their" : "its")} waiting-time target, or past it.",
            MonitoringEvent.PauseReviewPassed => days == 0
                ? $"{count} paused {specialty}{(plural ? "referrals are" : "referral is")} past {(plural ? "their" : "its")} review date."
                : $"{count} paused {specialty}{(plural ? "referrals are" : "referral is")} {moreThan} past {(plural ? "their" : "its")} review date.",
            _ => throw new ArgumentOutOfRangeException(nameof(monitoringEvent), monitoringEvent, "Not a service alert.")
        };

        return message.Length <= MonitoringNotifications.MaxMessageLength
            ? message
            : string.Concat(message.AsSpan(0, MonitoringNotifications.MaxMessageLength - 1), "…");
    }

    private static string Days(int days) =>
        string.Create(CultureInfo.InvariantCulture, $"{days} {(days == 1 ? "day" : "days")}");
}
