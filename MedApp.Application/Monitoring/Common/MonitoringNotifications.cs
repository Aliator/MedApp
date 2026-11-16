using System.Globalization;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Notifications;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Monitoring.Common;

/// <summary>
/// The notification an admin gets when one of their monitoring rules matches. It's built only from structured values
/// and names admins chose: usernames, the priority, the template's and stage's names (from definitions admins write),
/// and dates. Never free text staff typed (the procedure's title, notes, block or cancel reasons) and never the patient:
/// the notification links to the procedure, where whoever opens it sees what their role allows (DPIA, risk 3).
/// </summary>
public static class MonitoringNotifications
{
    /// <summary>The longest message the notifications table takes.</summary>
    public const int MaxMessageLength = 500;

    public static Notification Create(Guid ownerUserId, MonitoredActivity activity, PatientProcedure procedure, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        UserId = ownerUserId,
        Kind = KindOf(activity.Event),
        Title = TitleOf(activity.Event),
        Message = Truncate(MessageOf(activity, procedure)),
        ProcedureId = procedure.Id,
        CreatedAtUtc = nowUtc
    };

    public static NotificationKind KindOf(MonitoringEvent monitoringEvent) => monitoringEvent switch
    {
        MonitoringEvent.ProcedureCreated => NotificationKind.MonitoredProcedureCreated,
        MonitoringEvent.StageFinished => NotificationKind.MonitoredStageFinished,
        MonitoringEvent.ProcedureCompleted => NotificationKind.MonitoredProcedureCompleted,
        MonitoringEvent.ProcedureBlocked => NotificationKind.MonitoredProcedureBlocked,
        MonitoringEvent.ProcedureCancelled => NotificationKind.MonitoredProcedureCancelled,
        MonitoringEvent.ProcedureAssigned => NotificationKind.MonitoredProcedureAssigned,
        MonitoringEvent.StageUnlocked => NotificationKind.MonitoredStageUnlocked,
        MonitoringEvent.DueDateChanged => NotificationKind.MonitoredDueDateChanged,
        _ => throw new ArgumentOutOfRangeException(nameof(monitoringEvent), monitoringEvent, null)
    };

    /// <summary>A fixed title for each event, so no free text ever reaches it.</summary>
    public static string TitleOf(MonitoringEvent monitoringEvent) => monitoringEvent switch
    {
        MonitoringEvent.ProcedureCreated => "Procedure created",
        MonitoringEvent.StageFinished => "Stage finished",
        MonitoringEvent.ProcedureCompleted => "Procedure completed",
        MonitoringEvent.ProcedureBlocked => "Procedure blocked",
        MonitoringEvent.ProcedureCancelled => "Procedure cancelled",
        MonitoringEvent.ProcedureAssigned => "Staff assigned",
        MonitoringEvent.StageUnlocked => "Stage unlocked",
        MonitoringEvent.DueDateChanged => "Due date changed",
        _ => throw new ArgumentOutOfRangeException(nameof(monitoringEvent), monitoringEvent, null)
    };

    /// <summary>What happened, e.g. "sam finished the Bloods stage on a High priority Blood tests procedure."</summary>
    public static string MessageOf(MonitoredActivity activity, PatientProcedure procedure)
    {
        var who = activity.ActorUsername;
        var what = Describe(procedure);
        var stage = activity.StageName is { Length: > 0 } name ? $"the {name} stage" : "a stage";

        return activity.Event switch
        {
            MonitoringEvent.ProcedureCreated => $"{who} created {what}.",
            MonitoringEvent.StageFinished => $"{who} finished {stage} on {what}.",
            MonitoringEvent.ProcedureCompleted => $"{who} completed {what}.",
            MonitoringEvent.ProcedureBlocked => $"{who} blocked {what}.",
            MonitoringEvent.ProcedureCancelled => $"{who} cancelled {what}.",
            MonitoringEvent.ProcedureAssigned => activity.SubjectUsername == who
                ? $"{who} assigned themselves to {what}."
                : $"{who} assigned {Named(activity.SubjectUsername)} to {what}.",
            MonitoringEvent.StageUnlocked => $"{who} unlocked {stage} on {what}.",
            MonitoringEvent.DueDateChanged => activity is { DueDateFrom: { } from, DueDateTo: { } to }
                ? $"{who} moved the due date of {what} from {Format(from)} to {Format(to)}."
                : $"{who} changed the due date of {what}.",
            _ => throw new ArgumentOutOfRangeException(nameof(activity), activity.Event, null)
        };
    }

    // "a High priority Blood tests procedure", or "an Urgent priority procedure" when it has no template.
    private static string Describe(PatientProcedure procedure)
    {
        var priority = procedure.Priority.ToString();
        var article = priority[0] is 'A' or 'E' or 'I' or 'O' or 'U' ? "an" : "a";
        var template = string.IsNullOrWhiteSpace(procedure.TemplateName) ? string.Empty : $" {procedure.TemplateName}";
        return $"{article} {priority} priority{template} procedure";
    }

    private static string Named(string username) => string.IsNullOrEmpty(username) ? "a deleted user" : username;

    // "22 Jun 2026": the invariant culture's month names, so it reads the same whatever the server's culture.
    private static string Format(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private static string Truncate(string message) =>
        message.Length <= MaxMessageLength ? message : string.Concat(message.AsSpan(0, MaxMessageLength - 1), "…");
}
