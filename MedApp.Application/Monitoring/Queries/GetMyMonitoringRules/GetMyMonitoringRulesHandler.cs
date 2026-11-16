using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Monitoring.Repositories;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Monitoring.Responses;
using MediatR;

namespace MedApp.Application.Monitoring.Queries.GetMyMonitoringRules;

public sealed class GetMyMonitoringRulesHandler(
    IMonitoringRuleRepository repository,
    IIdentityReadService identityReadService,
    IPatientProcedureStagesRepository stagesRepository)
    : IRequestHandler<GetMyMonitoringRulesQuery, IReadOnlyList<MonitoringRuleResponse>>
{
    public async Task<IReadOnlyList<MonitoringRuleResponse>> Handle(GetMyMonitoringRulesQuery request, CancellationToken cancellationToken)
    {
        var rules = await repository.GetForOwnerAsync(request.OwnerUserId, cancellationToken);
        var names = await MonitoringRuleNames.LoadAsync(rules, identityReadService, stagesRepository, cancellationToken);
        return MonitoringRuleMapper.ToResponse(rules, names);
    }
}
