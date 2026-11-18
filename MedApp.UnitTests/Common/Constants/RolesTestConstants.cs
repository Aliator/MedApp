using MedApp.Application.Authentication.Roles.Common;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>Synthetic roles for the role handlers' tests.</summary>
public static class RolesTestConstants
{
    public static readonly Guid RoleId = Guid.Parse("0000000f-0000-0000-0000-000000000001");
    public static readonly Guid AdminRoleId = Guid.Parse("0000000f-0000-0000-0000-000000000002");
    public static readonly Guid ActorId = Guid.Parse("0000000f-0000-0000-0000-000000000003");
    public static readonly Guid HolderId = Guid.Parse("0000000f-0000-0000-0000-000000000004");
    public static readonly DateTime CreatedAt = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime Now = new(2026, 11, 18, 10, 30, 0, DateTimeKind.Utc);

    public const string ActorUsername = "test.admin";
    public const string HolderUsername = "test.nurse";
    public const string RoleName = "Synthetic ward team";
    public const string OtherRoleName = "Synthetic clinic team";
    public const string Description = "Synthetic staff on the ward.";
    public const string RoleVersion = "synthetic-version-1";

    /// <summary>An editable role granting <c>ViewPatients</c> and <c>WorkPathways</c>.</summary>
    public static RoleData Role(params string[] permissions) =>
        new()
        {
            Id = RoleId,
            Name = RoleName,
            Description = Description,
            Version = RoleVersion,
            CreatedAt = CreatedAt,
            LastUpdated = CreatedAt,
            StoredPermissions = permissions.Length == 0 ? ["ViewPatients", "WorkPathways"] : permissions,
        };

    /// <summary>The built-in Admin role.</summary>
    public static RoleData AdminRole() =>
        new()
        {
            Id = AdminRoleId,
            Name = "Admin",
            Description = "Built in.",
            IsBuiltIn = true,
            Version = RoleVersion,
            CreatedAt = CreatedAt,
            LastUpdated = CreatedAt,
        };
}
