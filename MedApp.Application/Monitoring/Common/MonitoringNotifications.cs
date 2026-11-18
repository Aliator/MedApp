using System.Globalization;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Monitoring.Common;

/// <summary>
/// The notification an admin gets when one of their monitoring rules matches. It's built only from structured values
/// and names admins chose: usernames, the priority, the template's and stage's names (from definitions admins write),
/// and dates. Never free text staff typed (the pathway's title, notes, block or cancel reasons) and never the patient:
/// the notification links to the pathway, where whoever opens it sees what their role allows (DPIA, risk 3).
/// </summary>
public static class MonitoringNotifications
{
    /// <summary>The longest message the notifications table takes.</summary>
    public const int MaxMessageLength = 500;

    public static Notification Create(Guid ownerUserId, MonitoredActivity activity, PatientPathway pathway, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        UserId = ownerUserId,
        Kind = KindOf(activity.Event),
        Title = TitleOf(activity.Event),
        Message = Truncate(MessageOf(activity, pathway)),
        PathwayId = pathway.Id,
        CreatedAtUtc = nowUtc
    };

    public static NotificationKind KindOf(MonitoringEvent monitoringEvent) => monitoringEvent switch
    {
        MonitoringEvent.PathwayCreated => NotificationKind.MonitoredPathwayCreated,
        MonitoringEvent.StageFinished => NotificationKind.MonitoredStageFinished,
        MonitoringEvent.PathwayCompleted => NotificationKind.MonitoredPathwayCompleted,
        MonitoringEvent.PathwayBlocked => NotificationKind.MonitoredPathwayBlocked,
        MonitoringEvent.PathwayCancelled => NotificationKind.MonitoredPathwayCancelled,
        MonitoringEvent.PathwayAssigned => NotificationKind.MonitoredPathwayAssigned,
        MonitoringEvent.StageUnlocked => NotificationKind.MonitoredStageUnlocked,
        MonitoringEvent.DueDateChanged => NotificationKind.MonitoredDueDateChanged,
        _ => throw new ArgumentOutOfRangeException(nameof(monitoringEvent), monitoringEvent, null)
    };

    /// <summary>A fixed title for each event, so no free text ever reaches it.</summary>
    public static string TitleOf(MonitoringEvent monitoringEvent) => monitoringEvent switch
    {
        MonitoringEvent.PathwayCreated => "Pathway created",
        MonitoringEvent.StageFinished => "Stage finished",
        MonitoringEvent.PathwayCompleted => "Pathway completed",
        MonitoringEvent.PathwayBlocked => "Pathway blocked",
        MonitoringEvent.PathwayCancelled => "Pathway cancelled",
        MonitoringEvent.PathwayAssigned => "Staff assigned",
        MonitoringEvent.StageUnlocked => "Stage unlocked",
        MonitoringEvent.DueDateChanged => "Target date changed",
        _ => throw new ArgumentOutOfRangeException(nameof(monitoringEvent), monitoringEvent, null)
    };

    /// <summary>What happened, e.g. "sam finished the Bloods stage on a High priority Blood tests pathway."</summary>
    public static string MessageOf(MonitoredActivity activity, PatientPathway pathway)
    {
        var who = activity.ActorUsername;
        var what = Describe(pathway);
        var stage = activity.StageName is { Length: > 0 } name ? $"the {name} stage" : "a stage";

        return activity.Event switch
        {
            MonitoringEvent.PathwayCreated => $"{who} created {what}.",
            MonitoringEvent.StageFinished => $"{who} finished {stage} on {what}.",
            MonitoringEvent.PathwayCompleted => $"{who} completed {what}.",
            MonitoringEvent.PathwayBlocked => $"{who} blocked {what}.",
            MonitoringEvent.PathwayCancelled => $"{who} cancelled {what}.",
            MonitoringEvent.PathwayAssigned => activity.SubjectUsername == who
                ? $"{who} assigned themselves to {what}."
                : $"{who} assigned {Named(activity.SubjectUsername)} to {what}.",
            MonitoringEvent.StageUnlocked => $"{who} unlocked {stage} on {what}.",
            MonitoringEvent.DueDateChanged => activity is { DueDateFrom: { } from, DueDateTo: { } to }
                ? $"{who} moved the target date of {what} from {Format(from)} to {Format(to)}."
                : $"{who} changed the target date of {what}.",
            _ => throw new ArgumentOutOfRangeException(nameof(activity), activity.Event, null)
        };
    }

    // "a High priority Blood tests pathway", or "an Urgent priority pathway" when it has no template.
    private static string Describe(PatientPathway pathway)
    {
        var priority = pathway.Priority.ToString();
        var article = priority[0] is 'A' or 'E' or 'I' or 'O' or 'U' ? "an" : "a";
        var template = string.IsNullOrWhiteSpace(pathway.TemplateName) ? string.Empty : $" {pathway.TemplateName}";
        return $"{article} {priority} priority{template} pathway";
    }

    private static string Named(string username) => string.IsNullOrEmpty(username) ? "a deleted user" : username;

    // "22 Jun 2026": the invariant culture's month names, so it reads the same whatever the server's culture.
    private static string Format(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private static string Truncate(string message) =>
        message.Length <= MaxMessageLength ? message : string.Concat(message.AsSpan(0, MaxMessageLength - 1), "…");
}
