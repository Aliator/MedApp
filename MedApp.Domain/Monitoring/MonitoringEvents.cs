namespace MedApp.Domain.Monitoring;

/// <summary>Groups of <see cref="MonitoringEvent"/>s.</summary>
public static class MonitoringEvents
{
    /// <summary>The service alerts: time-based events the service-alerts job finds, each against a rule's days.</summary>
    public static readonly IReadOnlyList<MonitoringEvent> ServiceAlerts =
    [
        MonitoringEvent.ReferralNotTriaged,
        MonitoringEvent.ResultNotSignedOff,
        MonitoringEvent.VisitNotBooked,
        MonitoringEvent.SpecimenOverdue,
        MonitoringEvent.WaitBreached,
        MonitoringEvent.PauseReviewPassed
    ];

    /// <summary>Whether the event is a service alert (time-based, with days) rather than a change to a pathway.</summary>
    public static bool IsServiceAlert(MonitoringEvent monitoringEvent) =>
        monitoringEvent is >= MonitoringEvent.ReferralNotTriaged and <= MonitoringEvent.PauseReviewPassed;
}
