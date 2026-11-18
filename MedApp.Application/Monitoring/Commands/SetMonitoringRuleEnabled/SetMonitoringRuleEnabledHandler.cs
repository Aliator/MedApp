using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Monitoring.Repositories;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Monitoring.Responses;
using MediatR;

namespace MedApp.Application.Monitoring.Commands.SetMonitoringRuleEnabled;

public sealed class SetMonitoringRuleEnabledHandler(
    IMonitoringRuleRepository repository,
    IIdentityReadService identityReadService,
    IPatientPathwayStagesRepository stagesRepository,
    ITeamRepository teamRepository,
    TimeProvider timeProvider)
    : IRequestHandler<SetMonitoringRuleEnabledCommand, MonitoringRuleResponse>
{
    public async Task<MonitoringRuleResponse> Handle(SetMonitoringRuleEnabledCommand request, CancellationToken cancellationToken)
    {
        var rule = await repository.GetAsync(request.Id, request.OwnerUserId, cancellationToken)
                   ?? throw new NotFoundException($"Monitoring rule '{request.Id}' was not found.");

        if (rule.IsEnabled != request.IsEnabled)
        {
            rule.IsEnabled = request.IsEnabled;
            rule.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            await repository.UpdateAsync(rule, cancellationToken);
        }

        var names = await MonitoringRuleNames.LoadAsync([rule], identityReadService, stagesRepository, teamRepository, cancellationToken);
        return MonitoringRuleMapper.ToResponse(rule, names);
    }
}
