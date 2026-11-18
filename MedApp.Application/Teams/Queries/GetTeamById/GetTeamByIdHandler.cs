using MedApp.Application.Teams.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Teams.Responses;
using MediatR;

namespace MedApp.Application.Teams.Queries.GetTeamById;

public sealed class GetTeamByIdHandler(ITeamRepository repository)
    : IRequestHandler<GetTeamByIdQuery, TeamResponse>
{
    public async Task<TeamResponse> Handle(GetTeamByIdQuery request, CancellationToken cancellationToken)
    {
        var team = await repository.GetOrThrowAsync(request.TeamId, cancellationToken);
        return await TeamMapper.ToResponseAsync(team, repository, cancellationToken);
    }
}
