using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Monitoring.Repositories;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Specialties.Repositories;
using MedApp.Contracts.Monitoring.Responses;
using MedApp.Domain.Monitoring;
using MediatR;

namespace MedApp.Application.Monitoring.Commands.CreateMonitoringRule;

public sealed class CreateMonitoringRuleHandler(
    IMonitoringRuleRepository repository,
    IIdentityReadService identityReadService,
    IPermissionHolders permissionHolders,
    IPatientPathwayStagesRepository stagesRepository,
    ISpecialtyRepository specialtyRepository,
    TimeProvider timeProvider)
    : IRequestHandler<CreateMonitoringRuleCommand, MonitoringRuleResponse>
{
    public async Task<MonitoringRuleResponse> Handle(CreateMonitoringRuleCommand request, CancellationToken cancellationToken)
    {
        if (await repository.CountForOwnerAsync(request.OwnerUserId, cancellationToken) >= MonitoringRuleInput.MaxRulesPerAdmin)
            throw new ConflictException(
                $"You have {MonitoringRuleInput.MaxRulesPerAdmin} rules, the most allowed. Delete one you no longer need first.");

        await MonitoringRuleInput.CheckAsync(request, existing: null, identityReadService, permissionHolders, stagesRepository, specialtyRepository, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var rule = new MonitoringRule
        {
            Id = Guid.NewGuid(),
            OwnerUserId = request.OwnerUserId,
            CreatedAtUtc = now
        };
        MonitoringRuleInput.Apply(rule, request, now);

        await repository.AddAsync(rule, cancellationToken);

        var names = await MonitoringRuleNames.LoadAsync([rule], identityReadService, stagesRepository, specialtyRepository, cancellationToken);
        return MonitoringRuleMapper.ToResponse(rule, names);
    }
}
