using MedApp.Application.Authentication.Users.Common;
using MedApp.Application.Common.Identity;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Users.Queries.GetUserByUsername;

public sealed class GetUserByUsernameHandler(
    IIdentityReadService identityReadService,
    ITeamRepository teams)
    : IRequestHandler<GetUserByUsernameQuery, UserResponse?>
{
    public async Task<UserResponse?> Handle(
        GetUserByUsernameQuery request,
        CancellationToken ct)
    {
        var user = await identityReadService.GetUserAsync(request.Username, ct);
        return user is null ? null : await UserAccountTypes.WithAccountTypeAsync(user, teams, ct);
    }
}