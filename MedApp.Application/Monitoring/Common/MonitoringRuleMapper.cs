using MedApp.Contracts.Monitoring.Responses;
using MedApp.Domain.Monitoring;

namespace MedApp.Application.Monitoring.Common;

public static class MonitoringRuleMapper
{
    /// <summary>
    /// A rule, with the names of what it names now: null for "any" (no ID), and null for a deleted account, template
    /// or stage (an ID the names don't have).
    /// </summary>
    public static MonitoringRuleResponse ToResponse(MonitoringRule rule, MonitoringRuleNames names) => new(
        rule.Id,
        rule.SubjectUserId,
        NameOf(rule.SubjectUserId, names.Usernames),
        rule.Event.ToString(),
        rule.MinimumPriority?.ToString(),
        rule.TemplateId,
        NameOf(rule.TemplateId, names.TemplateNames),
        rule.StageDefinitionId,
        NameOf(rule.StageDefinitionId, names.StageNames),
        rule.ThresholdDays,
        rule.SpecialtyId,
        NameOf(rule.SpecialtyId, names.SpecialtyNames),
        rule.IsEnabled,
        rule.CreatedAtUtc,
        rule.UpdatedAtUtc);

    public static IReadOnlyList<MonitoringRuleResponse> ToResponse(IEnumerable<MonitoringRule> rules, MonitoringRuleNames names) =>
        rules.Select(rule => ToResponse(rule, names)).ToList();

    private static string? NameOf(Guid? id, IReadOnlyDictionary<Guid, string> names) =>
        id is { } key ? names.GetValueOrDefault(key) : null;
}
