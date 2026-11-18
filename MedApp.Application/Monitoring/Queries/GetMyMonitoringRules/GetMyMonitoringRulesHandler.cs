using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Monitoring.Repositories;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Specialties.Repositories;
using MedApp.Contracts.Monitoring.Responses;
using MediatR;

namespace MedApp.Application.Monitoring.Queries.GetMyMonitoringRules;

public sealed class GetMyMonitoringRulesHandler(
    IMonitoringRuleRepository repository,
    IIdentityReadService identityReadService,
    IPatientPathwayStagesRepository stagesRepository,
    ISpecialtyRepository specialtyRepository)
    : IRequestHandler<GetMyMonitoringRulesQuery, IReadOnlyList<MonitoringRuleResponse>>
{
    public async Task<IReadOnlyList<MonitoringRuleResponse>> Handle(GetMyMonitoringRulesQuery request, CancellationToken cancellationToken)
    {
        var rules = await repository.GetForOwnerAsync(request.OwnerUserId, cancellationToken);
        var names = await MonitoringRuleNames.LoadAsync(rules, identityReadService, stagesRepository, specialtyRepository, cancellationToken);
        return MonitoringRuleMapper.ToResponse(rules, names);
    }
}
