using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Monitoring.Repositories;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Monitoring.Responses;
using MediatR;

namespace MedApp.Application.Monitoring.Commands.UpdateMonitoringRule;

public sealed class UpdateMonitoringRuleHandler(
    IMonitoringRuleRepository repository,
    IIdentityReadService identityReadService,
    IPatientPathwayStagesRepository stagesRepository,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateMonitoringRuleCommand, MonitoringRuleResponse>
{
    public async Task<MonitoringRuleResponse> Handle(UpdateMonitoringRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await repository.GetAsync(request.Id, request.OwnerUserId, cancellationToken)
                   ?? throw new NotFoundException($"Monitoring rule '{request.Id}' was not found.");

        // Only what this edit newly names has to exist: a rule may keep naming something since deleted.
        await MonitoringRuleInput.CheckAsync(request, rule, identityReadService, stagesRepository, cancellationToken);

        MonitoringRuleInput.Apply(rule, request, timeProvider.GetUtcNow().UtcDateTime);
        await repository.UpdateAsync(rule, cancellationToken);

        var names = await MonitoringRuleNames.LoadAsync([rule], identityReadService, stagesRepository, cancellationToken);
        return MonitoringRuleMapper.ToResponse(rule, names);
    }
}
