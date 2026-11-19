using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Teams;
using MedApp.Application.Consults.Common;
using MedApp.Application.Consults.Repositories;
using MedApp.Contracts.Consults.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Consults.Queries.GetIncomingConsults;

public sealed class GetIncomingConsultsHandler(
    IConsultRepository repository,
    IActivityLog activityLog)
    : IRequestHandler<GetIncomingConsultsQuery, IReadOnlyList<ConsultResponse>>
{
    public async Task<IReadOnlyList<ConsultResponse>> Handle(GetIncomingConsultsQuery request, CancellationToken cancellationToken)
    {
        var scope = request.Viewer.Teams;
        TeamPicker.EnsureCanPick(request.TeamId, scope);

        var consults = await repository.GetIncomingAsync(request.TeamId, scope, cancellationToken);
        await activityLog.RecordViewsAsync(
            ActivityAction.PatientListed, consults.Select(c => c.PatientId).Distinct(), cancellationToken,
            details: TeamPicker.ListDetails(request.TeamId, scope));

        return consults.Select(c => ConsultMapper.ToResponse(c, request.Viewer)).ToList();
    }
}
