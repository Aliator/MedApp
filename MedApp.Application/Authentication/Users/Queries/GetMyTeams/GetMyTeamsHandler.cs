using MedApp.Application.Teams.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Teams.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Users.Queries.GetMyTeams;

public sealed class GetMyTeamsHandler(ITeamRepository repository)
    : IRequestHandler<GetMyTeamsQuery, IReadOnlyList<TeamMembershipResponse>>
{
    public async Task<IReadOnlyList<TeamMembershipResponse>> Handle(
        GetMyTeamsQuery request,
        CancellationToken cancellationToken) =>
        TeamMapper.ToStaffResponses(await repository.GetForUserAsync(request.UserId, cancellationToken));
}
