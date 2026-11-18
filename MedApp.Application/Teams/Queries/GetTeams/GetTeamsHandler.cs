using MedApp.Application.Teams.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Teams.Responses;
using MediatR;

namespace MedApp.Application.Teams.Queries.GetTeams;

public sealed class GetTeamsHandler(ITeamRepository repository)
    : IRequestHandler<GetTeamsQuery, IReadOnlyList<TeamResponse>>
{
    public async Task<IReadOnlyList<TeamResponse>> Handle(GetTeamsQuery request, CancellationToken cancellationToken)
    {
        var teams = await repository.GetAllAsync(request.IncludeArchived, cancellationToken);
        var usage = await repository.GetUsageAsync(teams.Select(s => s.Id), cancellationToken);

        return teams
            .Select(s => TeamMapper.ToResponse(s, usage.GetValueOrDefault(s.Id, TeamUsage.None)))
            .ToList();
    }
}
