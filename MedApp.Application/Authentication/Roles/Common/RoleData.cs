namespace MedApp.Application.Authentication.Roles.Common;

/// <summary>
/// A role as the role handlers see it: Identity's role (<c>ApplicationRole</c>, an Infrastructure type) with its stored
/// permission names. <see cref="Version"/> is its concurrency stamp, which every change replaces, so a change made from
/// an older copy is refused (409).
/// </summary>
public sealed record RoleData
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool IsArchived { get; init; }

    /// <summary>The built-in Admin role: its permissions are fixed in code, and it can't be changed or deleted.</summary>
    public bool IsBuiltIn { get; init; }

    /// <summary>Identity's <c>ConcurrencyStamp</c>: sent to the page, and back as <c>expectedVersion</c>.</summary>
    public string Version { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime LastUpdated { get; init; }

    /// <summary>
    /// The permission names stored for it (none for the built-in role), as they are, including any code no longer
    /// knows or that an editable role can't have: those grant nothing (<c>Permissions.GrantedBy</c>).
    /// </summary>
    public IReadOnlyList<string> StoredPermissions { get; init; } = [];
}

/// <summary>Someone who holds a role.</summary>
public sealed record RoleHolder(Guid UserId, string Username);
