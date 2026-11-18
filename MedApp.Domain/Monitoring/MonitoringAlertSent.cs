namespace MedApp.Domain.Monitoring;

/// <summary>
/// That a service alert rule has reported an item (a referral, result, appointment, specimen or clock pause), so it
/// isn't reported again under that rule (decision 135). Saved with the alert's notification, in one save. Holds IDs and
/// a time only: nothing about the patient.
/// </summary>
public sealed class MonitoringAlertSent
{
    public Guid RuleId { get; init; }

    /// <summary>The item reported; what it is follows from the rule's event.</summary>
    public Guid SubjectId { get; init; }

    public DateTime SentAtUtc { get; init; }
}
