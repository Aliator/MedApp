using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Teams.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Teams.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Users.Queries.GetUserTeams;

public sealed class GetUserTeamsHandler(IIdentityReadService readService, ITeamRepository repository)
    : IRequestHandler<GetUserTeamsQuery, IReadOnlyList<TeamMembershipResponse>>
{
    public async Task<IReadOnlyList<TeamMembershipResponse>> Handle(
        GetUserTeamsQuery request,
        CancellationToken cancellationToken)
    {
        var user = await readService.GetUserAsync(request.Username, cancellationToken)
                   ?? throw new NotFoundException($"User '{request.Username}' was not found.");

        return TeamMapper.ToStaffResponses(await repository.GetMembershipsAsync(user.UserId, cancellationToken));
    }
}
