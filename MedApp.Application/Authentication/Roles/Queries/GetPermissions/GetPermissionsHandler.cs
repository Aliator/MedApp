using MedApp.Application.Authentication.Roles.Common;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Roles.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Roles.Queries.GetPermissions;

public sealed class GetPermissionsHandler : IRequestHandler<GetPermissionsQuery, IReadOnlyList<PermissionResponse>>
{
    public Task<IReadOnlyList<PermissionResponse>> Handle(GetPermissionsQuery request, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PermissionResponse>>(Permissions.Catalogue.Select(RoleMapper.ToResponse).ToList());
}
