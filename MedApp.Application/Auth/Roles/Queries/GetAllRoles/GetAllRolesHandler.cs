using MedApp.Application.Common.Identity;
using MediatR;

namespace MedApp.Application.Auth.Roles.Queries.GetAllRoles;

public sealed class GetAllRolesHandler(
    IIdentityReadService identityReadService)
    : IRequestHandler<GetAllRolesQuery, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(
        GetAllRolesQuery request,
        CancellationToken ct)
    {
        return await identityReadService.GetRoleNamesAsync(ct);
    }
}