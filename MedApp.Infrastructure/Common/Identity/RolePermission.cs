namespace MedApp.Infrastructure.Common.Identity;

/// <summary>
/// One permission a role grants, by its name (<see cref="Application.Common.Identity.Permission"/>), never its number,
/// so code can reorder the list. A name code no longer knows grants nothing. The built-in Admin role has none: its
/// permissions are fixed in code.
/// </summary>
public sealed class RolePermission
{
    public const int PermissionMaxLength = 64;

    public Guid RoleId { get; set; }

    public string Permission { get; set; } = string.Empty;
}
