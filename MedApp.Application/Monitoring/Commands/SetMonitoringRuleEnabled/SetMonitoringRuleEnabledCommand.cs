using MedApp.Contracts.Monitoring.Responses;
using MediatR;

namespace MedApp.Application.Monitoring.Commands.SetMonitoringRuleEnabled;

/// <summary>
/// Switches one of the signed-in admin's rules on or off, changing nothing else, so a rule naming something since
/// deleted can still be switched. Another admin's rule is a 404, as if it didn't exist.
/// </summary>
public sealed record SetMonitoringRuleEnabledCommand(Guid Id, Guid OwnerUserId, bool IsEnabled) : IRequest<MonitoringRuleResponse>;
