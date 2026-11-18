using MedApp.Application.Authentication.Roles.Common;
using MedApp.Application.Authentication.Roles.Repositories;
using MedApp.Contracts.Roles.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Roles.Queries.GetRoles;

public sealed class GetRolesHandler(IRoleRepository roles)
    : IRequestHandler<GetRolesQuery, IReadOnlyList<RoleResponse>>
{
    public async Task<IReadOnlyList<RoleResponse>> Handle(GetRolesQuery request, CancellationToken ct)
    {
        var all = await roles.GetAllAsync(ct);
        var counts = await roles.CountHoldersAsync(ct);
        return RoleMapper.ToResponses(all, counts);
    }
}
