using MedApp.Contracts.Monitoring.Responses;
using MediatR;

namespace MedApp.Application.Monitoring.Queries.GetMyMonitoringRules;

/// <summary>The signed-in admin's monitoring rules, oldest first. Admins see only their own.</summary>
public sealed record GetMyMonitoringRulesQuery(Guid OwnerUserId) : IRequest<IReadOnlyList<MonitoringRuleResponse>>;
