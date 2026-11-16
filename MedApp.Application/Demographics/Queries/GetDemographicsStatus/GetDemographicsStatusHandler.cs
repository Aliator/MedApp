using MedApp.Application.Demographics.Common;
using MedApp.Contracts.Demographics.Responses;
using MediatR;

namespace MedApp.Application.Demographics.Queries.GetDemographicsStatus;

public sealed class GetDemographicsStatusHandler(IPersonalDemographicsService demographics)
    : IRequestHandler<GetDemographicsStatusQuery, DemographicsStatusResponse>
{
    public Task<DemographicsStatusResponse> Handle(GetDemographicsStatusQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(DemographicsMapper.ToStatus(demographics));
}
