using MedApp.Contracts.Roles.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Roles.Queries.GetPermissions;

/// <summary>The permission catalogue: each permission's name, group, description and whether it's for Admin only.</summary>
public sealed record GetPermissionsQuery : IRequest<IReadOnlyList<PermissionResponse>>;
