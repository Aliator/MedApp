namespace MedApp.Contracts.Monitoring.Responses;

/// <summary>
/// One of the signed-in admin's monitoring rules. Each name is the one now (null for "any" when its ID is null too,
/// and null for a deleted account, template or stage when its ID is set).
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
    bool IsEnabled,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
