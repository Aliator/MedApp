using MedApp.Application.Monitoring.Common;
using MedApp.Contracts.Monitoring.Responses;
using MediatR;

namespace MedApp.Application.Monitoring.Commands.CreateMonitoringRule;

/// <summary>Adds a monitoring rule for the signed-in admin (the owner, who is notified).</summary>
public sealed record CreateMonitoringRuleCommand(
    Guid OwnerUserId,
    Guid? SubjectUserId,
    string Event,
    string? MinimumPriority,
    Guid? TemplateId,
    Guid? StageDefinitionId,
    bool IsEnabled) : IRequest<MonitoringRuleResponse>, IMonitoringRuleInput;
