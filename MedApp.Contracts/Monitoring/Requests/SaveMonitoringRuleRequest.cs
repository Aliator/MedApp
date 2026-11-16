namespace MedApp.Contracts.Monitoring.Requests;

/// <summary>
/// A monitoring rule, to add one or to replace one of yours: who and what to watch for, the conditions, and whether
/// it's on. Leave a condition out (null) for "any".
/// </summary>
public sealed class SaveMonitoringRuleRequest
{
    /// <summary>The staff member to watch (for ProcedureAssigned, the one assigned); null for anyone.</summary>
    public Guid? SubjectUserId { get; set; }

    /// <summary>
    /// ProcedureCreated, StageFinished, ProcedureCompleted, ProcedureBlocked, ProcedureCancelled, ProcedureAssigned,
    /// StageUnlocked or DueDateChanged.
    /// </summary>
    public string Event { get; set; } = string.Empty;

    /// <summary>Low, Medium, High or Urgent: only procedures of this priority or higher. Null for any.</summary>
    public string? MinimumPriority { get; set; }

    /// <summary>Only procedures from this template; null for any.</summary>
    public Guid? TemplateId { get; set; }

    /// <summary>Only this stage (StageFinished and StageUnlocked only); null for any stage.</summary>
    public Guid? StageDefinitionId { get; set; }

    public bool IsEnabled { get; set; } = true;
}
