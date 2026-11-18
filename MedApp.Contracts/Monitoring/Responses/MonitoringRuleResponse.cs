namespace MedApp.Contracts.Monitoring.Responses;

/// <summary>
/// One of the signed-in admin's monitoring rules. Each name is the one now (null for "any" when its ID is null too,
/// and null for a deleted account, template, stage or team when its ID is set). <c>ThresholdDays</c> is a service
/// alert's days, null for the other events.
/// </summary>
public sealed record MonitoringRuleResponse(
    Guid Id,
    Guid? SubjectUserId,
    string? SubjectUsername,
    string Event,
    string? MinimumPriority,
    Guid? TemplateId,
    string? TemplateName,
    Guid? StageDefinitionId,
    string? StageName,
    int? ThresholdDays,
    Guid? TeamId,
    string? TeamName,
    bool IsEnabled,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
