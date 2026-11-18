using MedApp.Application.Common.Identity;

namespace MedApp.Application.Authentication.Roles.Common;

/// <summary>
/// What a change to a role did, for its activity log entry (<c>RoleChanged</c>), the application log and the notice
/// to the other admins: the role, its name before if renamed, whether its description changed (never the text, which
/// stays on the role), the permission names added and removed, and whether it was archived or restored. Role names and
/// permission names only: never patient data.
/// </summary>
public sealed record RoleChange(
    Guid RoleId,
    string Role,
    string? RenamedFrom,
    bool DescriptionChanged,
    IReadOnlyList<Permission> Added,
    IReadOnlyList<Permission> Removed,
    bool? Archived)
{
    /// <summary>Whether anything changed at all (a save that changes nothing records nothing).</summary>
    public bool Any => RenamedFrom is not null || DescriptionChanged || Added.Count > 0 || Removed.Count > 0 || Archived is not null;

    /// <summary>From the role as saved to what's asked for: <paramref name="permissions"/> as <see cref="RoleRules.Requested"/> gives them.</summary>
    public static RoleChange Between(RoleData before, string name, string description, IReadOnlyList<Permission> permissions)
    {
        var granted = RoleRules.Granted(before);
        return new RoleChange(
            before.Id,
            name,
            string.Equals(before.Name, name, StringComparison.Ordinal) ? null : before.Name,
            !string.Equals(before.Description, description, StringComparison.Ordinal),
            permissions.Except(granted).ToList(),
            granted.Except(permissions).ToList(),
            null);
    }

    /// <summary>Archiving (true) or restoring (false) a role.</summary>
    public static RoleChange Archiving(RoleData role, bool archived) =>
        new(role.Id, role.Name, null, false, [], [], archived);

    /// <summary>The activity log entry's details: only what changed, by name.</summary>
    public IReadOnlyDictionary<string, object?> Details()
    {
        var details = new Dictionary<string, object?> { ["roleId"] = RoleId, ["role"] = Role };
        if (RenamedFrom is not null)
            details["renamedFrom"] = RenamedFrom;
        if (DescriptionChanged)
            details["descriptionChanged"] = true;
        if (Added.Count > 0)
            details["added"] = Added.Select(p => p.ToString()).ToList();
        if (Removed.Count > 0)
            details["removed"] = Removed.Select(p => p.ToString()).ToList();
        if (Archived is { } archived)
            details["archived"] = archived;
        return details;
    }

    /// <summary>The change in a sentence, for the email to the other admins: "changed the role Nurse: added AssignPathways."</summary>
    public string Summary()
    {
        var parts = new List<string>();
        if (RenamedFrom is not null)
            parts.Add($"renamed it from {RenamedFrom}");
        if (DescriptionChanged)
            parts.Add("changed its description");
        if (Added.Count > 0)
            parts.Add($"added {string.Join(", ", Added)}");
        if (Removed.Count > 0)
            parts.Add($"removed {string.Join(", ", Removed)}");
        if (Archived is { } archived)
            parts.Add(archived ? "archived it" : "restored it");
        return $"changed the role {Role}: {string.Join("; ", parts)}.";
    }
}
