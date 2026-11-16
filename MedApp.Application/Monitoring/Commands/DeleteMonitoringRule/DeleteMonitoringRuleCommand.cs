using MediatR;

namespace MedApp.Application.Monitoring.Commands.DeleteMonitoringRule;

/// <summary>Deletes one of the signed-in admin's rules. Another admin's rule is a 404, as if it didn't exist.</summary>
public sealed record DeleteMonitoringRuleCommand(Guid Id, Guid OwnerUserId) : IRequest;
