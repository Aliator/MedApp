using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Domain.Monitoring;

/// <summary>
/// An admin's rule for being told about something staff do to pathways, e.g. "notify me when Sam finishes Bloods on
/// a High or Urgent priority pathway". When a change matches an enabled rule, its owner gets a notification. Each
/// rule belongs to one admin, who alone sees and changes it.
/// <para>
/// Like the audit log and notifications, it has no foreign keys: a rule naming a deleted account, template or stage
/// stays, shows what it named as deleted, and matches nothing more.
/// </para>
/// </summary>
public sealed class MonitoringRule
{
    public Guid Id { get; init; }

    /// <summary>The admin it belongs to, who is notified.</summary>
    public Guid OwnerUserId { get; init; }

    /// <summary>
    /// The staff member it watches (for <see cref="MonitoringEvent.PathwayAssigned"/>, the one assigned), or null for
    /// anyone.
    /// </summary>
    public Guid? SubjectUserId { get; set; }

    public MonitoringEvent Event { get; set; }

    /// <summary>Only pathways of this priority or higher, e.g. High for High and Urgent; null for any.</summary>
    public PatientPathwayPriority? MinimumPriority { get; set; }

    /// <summary>Only pathways from this template; null for any.</summary>
    public Guid? TemplateId { get; set; }

    /// <summary>
    /// Only stages made from this stage definition (for <see cref="MonitoringEvent.StageFinished"/> and
    /// <see cref="MonitoringEvent.StageUnlocked"/>); null for any stage.
    /// </summary>
    public Guid? StageDefinitionId { get; set; }

    /// <summary>
    /// For a service alert (a time-based event, <see cref="MonitoringEvents.IsServiceAlert"/>), the days, 0 to 90: how
    /// long something may wait before it's reported (for <see cref="MonitoringEvent.WaitBreached"/>, how many days
    /// before the target). Null for every other event.
    /// </summary>
    public int? ThresholdDays { get; set; }

    /// <summary>Only work of this specialty (the referral's, or the pathway's); null for any.</summary>
    public Guid? SpecialtyId { get; set; }

    /// <summary>Whether it notifies; a switched-off rule is kept but matches nothing.</summary>
    public bool IsEnabled { get; set; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; set; }
}
