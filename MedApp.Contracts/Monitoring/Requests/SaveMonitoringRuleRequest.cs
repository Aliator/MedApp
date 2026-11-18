namespace MedApp.Contracts.Monitoring.Requests;

/// <summary>
/// A monitoring rule, to add one or to replace one of yours: who and what to watch for, the conditions, and whether
/// it's on. Leave a condition out (null) for "any".
/// </summary>
public sealed class SaveMonitoringRuleRequest
{
    /// <summary>
    /// The staff member to watch (for PathwayAssigned, the one assigned; for ResultNotSignedOff, the responsible
    /// clinician); null for anyone. The other service alerts take none.
    /// </summary>
    public Guid? SubjectUserId { get; set; }

    /// <summary>
    /// PathwayCreated, StageFinished, PathwayCompleted, PathwayBlocked, PathwayCancelled, PathwayAssigned,
    /// StageUnlocked, DueDateChanged or StageFinishedWithoutVisit; or a service alert: ReferralNotTriaged,
    /// ResultNotSignedOff, VisitNotBooked, SpecimenOverdue, WaitBreached or PauseReviewPassed.
    /// </summary>
    public string Event { get; set; } = string.Empty;

    /// <summary>Low, Medium, High or Urgent: only pathways of this priority or higher. Null for any.</summary>
    public string? MinimumPriority { get; set; }

    /// <summary>Only pathways from this template; null for any.</summary>
    public Guid? TemplateId { get; set; }

    /// <summary>
    /// Only this stage (StageFinished, StageUnlocked, StageFinishedWithoutVisit, VisitNotBooked and SpecimenOverdue
    /// only); null for any stage.
    /// </summary>
    public Guid? StageDefinitionId { get; set; }

    /// <summary>
    /// A service alert's days, 0 to 90 (required for them, and only for them): how long something may wait before
    /// it's reported; for WaitBreached, how many days before the target date (0: once it's passed).
    /// </summary>
    public int? ThresholdDays { get; set; }

    /// <summary>Only work of this specialty (the referral's or the pathway's); null for any.</summary>
    public Guid? SpecialtyId { get; set; }

    public bool IsEnabled { get; set; } = true;
}
