using MedApp.Application.Common.Exceptions;
using MedApp.Application.Monitoring.Repositories;
using MediatR;

namespace MedApp.Application.Monitoring.Commands.DeleteMonitoringRule;

public sealed class DeleteMonitoringRuleHandler(IMonitoringRuleRepository repository)
    : IRequestHandler<DeleteMonitoringRuleCommand>
{
    public async Task Handle(DeleteMonitoringRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await repository.GetAsync(request.Id, request.OwnerUserId, cancellationToken)
                   ?? throw new NotFoundException($"Monitoring rule '{request.Id}' was not found.");

        await repository.DeleteAsync(rule, cancellationToken);
    }
}
