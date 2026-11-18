using MedApp.Application.Authentication.Roles.Common;
using MedApp.Application.Authentication.Roles.Repositories;
using MedApp.Contracts.Roles.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Roles.Queries.GetRoleById;

public sealed class GetRoleByIdHandler(IRoleRepository roles)
    : IRequestHandler<GetRoleByIdQuery, RoleResponse>
{
    public async Task<RoleResponse> Handle(GetRoleByIdQuery request, CancellationToken ct)
    {
        var role = await roles.GetOrThrowAsync(request.RoleId, ct);
        var holders = await roles.GetHoldersAsync(role.Id, ct);
        return RoleMapper.ToResponse(role, holders.Count, holders);
    }
}
