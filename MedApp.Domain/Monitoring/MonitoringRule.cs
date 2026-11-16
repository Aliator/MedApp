using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Domain.Monitoring;

/// <summary>
/// An admin's rule for being told about something staff do to procedures, e.g. "notify me when Sam finishes Bloods on
/// a High or Urgent priority procedure". When a change matches an enabled rule, its owner gets a notification. Each
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
    /// The staff member it watches (for <see cref="MonitoringEvent.ProcedureAssigned"/>, the one assigned), or null for
    /// anyone.
    /// </summary>
    public Guid? SubjectUserId { get; set; }

    public MonitoringEvent Event { get; set; }

    /// <summary>Only procedures of this priority or higher, e.g. High for High and Urgent; null for any.</summary>
    public PatientProcedurePriority? MinimumPriority { get; set; }

    /// <summary>Only procedures from this template; null for any.</summary>
    public Guid? TemplateId { get; set; }

    /// <summary>
    /// Only stages made from this stage definition (for <see cref="MonitoringEvent.StageFinished"/> and
    /// <see cref="MonitoringEvent.StageUnlocked"/>); null for any stage.
    /// </summary>
    public Guid? StageDefinitionId { get; set; }

    /// <summary>Whether it notifies; a switched-off rule is kept but matches nothing.</summary>
    public bool IsEnabled { get; set; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; set; }
}
