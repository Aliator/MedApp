using MedApp.Application.Authentication.Roles.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;

namespace MedApp.Application.Authentication.Roles.Common;

/// <summary>
/// The rules and messages for roles (<c>docs/centre-phase2-plan.md</c>, decisions 55 to 62): the built-in Admin role
/// can't be changed; editable roles can't have the Administrator-only permissions; a role someone holds is archived,
/// never deleted; a change starts from the version saved now.
/// </summary>
public static class RoleRules
{
    public const int MaxNameLength = 50;
    public const int MaxDescriptionLength = 200;

    public const string ChangedMeanwhileMessage =
        "Another admin changed this role while you had it open. Reload to see their change, then try again.";

    public const string BuiltInMessage = "The Admin role is built in: its name and permissions are fixed and it can't be archived or deleted.";

    public static string NameTakenMessage(string name) => $"A role called '{name}' already exists.";

    public static string HeldMessage(string name, int holders) =>
        $"{People(holders)} {(holders == 1 ? "holds" : "hold")} the role {name}, so it can't be deleted. Archive it instead, or take it from them first.";

    public static string UnknownPermissionMessage(string name) => $"'{name}' isn't a permission.";

    public static string AdminOnlyMessage(string name) =>
        $"{name} is for the Admin role only, so no other role can have it.";

    /// <summary>"1 person" or "6 people".</summary>
    public static string People(int count) => count == 1 ? "1 person" : $"{count} people";

    public static async Task<RoleData> GetOrThrowAsync(this IRoleRepository repository, Guid id, CancellationToken ct) =>
        await repository.GetAsync(id, ct) ?? throw new NotFoundException($"Role '{id}' was not found.");

    /// <summary>The built-in Admin role refuses every change (409).</summary>
    public static void EnsureEditable(RoleData role)
    {
        if (role.IsBuiltIn)
            throw new ConflictException(BuiltInMessage);
    }

    /// <summary>A change must start from the version saved now, or another admin has changed it since (409).</summary>
    public static void EnsureVersion(RoleData role, string expectedVersion)
    {
        if (!string.Equals(role.Version, expectedVersion, StringComparison.Ordinal))
            throw new ConflictException(ChangedMeanwhileMessage);
    }

    /// <summary>
    /// The permissions a role grants, as the page shows them (in the catalogue's order, without
    /// <see cref="Permission.UseApp"/>, which every role grants): Admin's fixed set, or the stored ones code knows and
    /// that aren't for Admin only.
    /// </summary>
    public static IReadOnlyList<Permission> Granted(RoleData role)
    {
        var granted = Permissions.GrantedBy(role.IsBuiltIn, role.StoredPermissions);
        return Permissions.Catalogue
            .Select(p => p.Permission)
            .Where(p => p != Permission.UseApp && granted.Contains(p))
            .ToList();
    }

    /// <summary>
    /// The permissions asked for, each once, in the catalogue's order, without <see cref="Permission.UseApp"/> (every
    /// role has it, so it's never stored). The validators have already refused unknown and Administrator-only names.
    /// </summary>
    public static IReadOnlyList<Permission> Requested(IEnumerable<string> names)
    {
        var wanted = names
            .Select(name => Permissions.TryParse(name?.Trim(), out var permission) ? permission : (Permission?)null)
            .OfType<Permission>()
            .ToHashSet();
        return Permissions.Catalogue
            .Select(p => p.Permission)
            .Where(p => p != Permission.UseApp && wanted.Contains(p))
            .ToList();
    }
}
