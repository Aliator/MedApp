using System.Globalization;
using MedApp.Application.Monitoring.Common;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Notifications;

namespace MedApp.Application.Monitoring.ServiceAlerts;

/// <summary>
/// A service alert's notification (decision 135): one per rule and run, e.g. "3 Dermatology referrals have waited more
/// than 2 days for triage." A fixed title; the message holds a count, days, and names admins chose (the team, and
/// for results the responsible clinician's username). Never a patient, a clinical value or free text. Its
/// <see cref="Notification.SubjectId"/> is the rule's team, so the web app opens the list filtered to it.
/// </summary>
public static class ServiceAlertNotifications
{
    public static Notification Create(
        MonitoringRule rule, int count, string? teamName, string? responsibleUsername, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        UserId = rule.OwnerUserId,
        Kind = KindOf(rule.Event),
        Title = TitleOf(rule.Event),
        Message = MessageOf(rule.Event, count, rule.ThresholdDays ?? 0, teamName, responsibleUsername),
        SubjectId = rule.TeamId,
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
        MonitoringEvent.LabCaseAtRisk => NotificationKind.MonitoredLabCaseAtRisk,
        MonitoringEvent.LabCaseOverdue => NotificationKind.MonitoredLabCaseOverdue,
        _ => throw new ArgumentOutOfRangeException(nameof(monitoringEvent), monitoringEvent, "Not a service alert.")
    };

    /// <summary>A fixed title for each alert.</summary>
    public static string TitleOf(MonitoringEvent monitoringEvent) => monitoringEvent switch
    {
        MonitoringEvent.ReferralNotTriaged => "Referrals awaiting triage",
        MonitoringEvent.ResultNotSignedOff => "Results awaiting sign-off",
        MonitoringEvent.VisitNotBooked => "Visits not booked",
        MonitoringEvent.SpecimenOverdue => "Samples overdue",
        MonitoringEvent.WaitBreached => "Waiting-time targets",
        MonitoringEvent.PauseReviewPassed => "Pause reviews passed",
        MonitoringEvent.LabCaseAtRisk => "Lab cases at risk",
        MonitoringEvent.LabCaseOverdue => "Lab cases overdue",
        _ => throw new ArgumentOutOfRangeException(nameof(monitoringEvent), monitoringEvent, "Not a service alert.")
    };

    /// <summary>What the alert found, e.g. "3 Dermatology referrals have waited more than 2 days for triage."</summary>
    public static string MessageOf(MonitoringEvent monitoringEvent, int count, int days, string? teamName, string? responsibleUsername)
    {
        var plural = count != 1;
        var team = string.IsNullOrWhiteSpace(teamName) ? string.Empty : $"{teamName} ";
        var have = plural ? "have" : "has";
        var are = plural ? "are" : "is";
        string Things(string one, string many) => $"{count} {team}{(plural ? many : one)}";
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
                ? $"{Things("sample", "samples")} {are} past {(plural ? "their" : "its")} expected turnaround."
                : $"{Things("sample", "samples")} {are} {moreThan} past {(plural ? "their" : "its")} expected turnaround.",
            MonitoringEvent.LabCaseAtRisk => days == 0
                ? $"{Things("lab case", "lab cases")} {are} at risk of missing {(plural ? "their" : "its")} due date."
                : $"{Things("lab case", "lab cases")} {have} been at risk of missing {(plural ? "their" : "its")} due date for {moreThan}.",
            MonitoringEvent.LabCaseOverdue => days == 0
                ? $"{Things("lab case", "lab cases")} {are} past {(plural ? "their" : "its")} due date."
                : $"{Things("lab case", "lab cases")} {are} {moreThan} past {(plural ? "their" : "its")} due date.",
            MonitoringEvent.WaitBreached => days == 0
                ? $"{Things("referral", "referrals")} {have} passed {(plural ? "their" : "its")} waiting-time target."
                : $"{Things("referral", "referrals")} {are} within {Days(days)} of {(plural ? "their" : "its")} waiting-time target, or past it.",
            MonitoringEvent.PauseReviewPassed => days == 0
                ? $"{count} paused {team}{(plural ? "referrals are" : "referral is")} past {(plural ? "their" : "its")} review date."
                : $"{count} paused {team}{(plural ? "referrals are" : "referral is")} {moreThan} past {(plural ? "their" : "its")} review date.",
            _ => throw new ArgumentOutOfRangeException(nameof(monitoringEvent), monitoringEvent, "Not a service alert.")
        };

        return message.Length <= MonitoringNotifications.MaxMessageLength
            ? message
            : string.Concat(message.AsSpan(0, MonitoringNotifications.MaxMessageLength - 1), "…");
    }

    private static string Days(int days) =>
        string.Create(CultureInfo.InvariantCulture, $"{days} {(days == 1 ? "day" : "days")}");
}
