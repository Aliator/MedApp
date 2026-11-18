using MedApp.Application.Common.Identity;
using MedApp.Contracts.Roles.Responses;

namespace MedApp.Application.Authentication.Roles.Common;

/// <summary>Roles and the permission catalogue as the API returns them.</summary>
public static class RoleMapper
{
    /// <summary>A role, with how many hold it, and who when <paramref name="holders"/> is given (a single role's page).</summary>
    public static RoleResponse ToResponse(RoleData role, int holderCount, IReadOnlyList<RoleHolder>? holders = null) =>
        new()
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            IsBuiltIn = role.IsBuiltIn,
            IsArchived = role.IsArchived,
            Permissions = RoleRules.Granted(role).Select(p => p.ToString()).ToList(),
            HolderCount = holderCount,
            Version = role.Version,
            CanEdit = !role.IsBuiltIn,
            CanDelete = !role.IsBuiltIn && holderCount == 0,
            Holders = holders?.Select(h => new RoleHolderResponse { UserId = h.UserId, Username = h.Username }).ToList(),
            CreatedAt = role.CreatedAt,
            LastUpdated = role.LastUpdated,
        };

    /// <summary>
    /// Every role in the order the page lists them: the built-in Admin role first, then the rest by name (archived
    /// ones too: the page marks them).
    /// </summary>
    public static IReadOnlyList<RoleResponse> ToResponses(IEnumerable<RoleData> roles, IReadOnlyDictionary<Guid, int> holderCounts) =>
        roles
            .OrderByDescending(role => role.IsBuiltIn)
            .ThenBy(role => role.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(role => role.Id)
            .Select(role => ToResponse(role, holderCounts.GetValueOrDefault(role.Id)))
            .ToList();

    /// <summary>A permission in the catalogue.</summary>
    public static PermissionResponse ToResponse(PermissionDescription permission) =>
        new()
        {
            Name = permission.Name,
            Group = permission.Group.ToString(),
            Description = permission.Description,
            IsAdminOnly = permission.IsAdminOnly,
        };
}
