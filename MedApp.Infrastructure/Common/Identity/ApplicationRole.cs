using Microsoft.AspNetCore.Identity;

namespace MedApp.Infrastructure.Common.Identity;

/// <summary>
/// A role: Identity's role (membership stays in <c>AspNetUserRoles</c>), with a description, whether it's archived
/// (hidden from the picker, so nobody new is given it; its holders keep its permissions) and whether it's the built-in
/// Admin role, whose permissions are fixed in code. Other roles' permissions are rows in <c>RolePermissions</c>.
/// </summary>
public sealed class ApplicationRole : IdentityRole<Guid>
{
    public const int NameMaxLength = 50;
    public const int DescriptionMaxLength = 200;

    public ApplicationRole()
    {
    }

    public ApplicationRole(string roleName) : base(roleName)
    {
    }

    public string Description { get; set; } = string.Empty;

    public bool IsArchived { get; set; }

    /// <summary>The Admin role: it can't be renamed, edited, archived or deleted, and has every permission but triage.</summary>
    public bool IsBuiltIn { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastUpdated { get; set; }

    public List<RolePermission> Permissions { get; set; } = [];
}
