using MedApp.Application.Authentication.Users.Common;
using MedApp.Application.Common.Identity;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Users.Queries.GetAllUsers;

public sealed class GetAllUsersHandler(IIdentityReadService identityReadService, ITeamRepository teams)
    : IRequestHandler<GetAllUsersQuery, IReadOnlyList<UserResponse>>
{
    public async Task<IReadOnlyList<UserResponse>> Handle(
        GetAllUsersQuery request,
        CancellationToken ct)
    {
        return await UserAccountTypes.WithAccountTypesAsync(await identityReadService.GetUsersAsync(ct), teams, ct);
    }
}