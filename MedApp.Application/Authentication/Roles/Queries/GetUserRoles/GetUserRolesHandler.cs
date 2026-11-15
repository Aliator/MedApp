using MedApp.Application.Common.Identity;
using MediatR;

namespace MedApp.Application.Authentication.Roles.Queries.GetUserRoles;

public sealed class GetUserRolesHandler(
    IIdentityReadService identityReadService)
    : IRequestHandler<GetUserRolesQuery, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(
        GetUserRolesQuery request,
        CancellationToken ct)
    {
        return await identityReadService.GetRolesForUserAsync(
            request.Username,
            ct);
    }
}