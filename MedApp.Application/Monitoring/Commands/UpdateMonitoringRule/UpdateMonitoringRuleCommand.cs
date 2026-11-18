using MedApp.Application.Monitoring.Common;
using MedApp.Contracts.Monitoring.Responses;
using MediatR;

namespace MedApp.Application.Monitoring.Commands.UpdateMonitoringRule;

/// <summary>
/// Replaces one of the signed-in admin's rules (switching it on or off too). Another admin's rule is a 404, as if it
/// didn't exist.
/// </summary>
public sealed record UpdateMonitoringRuleCommand(
    Guid Id,
    Guid OwnerUserId,
    Guid? SubjectUserId,
    string Event,
    string? MinimumPriority,
    Guid? TemplateId,
    Guid? StageDefinitionId,
    bool IsEnabled,
    int? ThresholdDays = null,
    Guid? TeamId = null) : IRequest<MonitoringRuleResponse>, IMonitoringRuleInput;
