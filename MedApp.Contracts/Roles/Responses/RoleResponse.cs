namespace MedApp.Contracts.Roles.Responses;

/// <summary>A role, with the permissions it grants and how many people hold it.</summary>
public sealed class RoleResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>The Admin role: built in, with permissions fixed in code. It can't be changed, archived or deleted.</summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>Archived roles can't be given to anyone new; the people who hold one keep its permissions.</summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// The permissions it grants, by name, in the catalogue's order. Every role also grants <c>UseApp</c>, which isn't
    /// listed.
    /// </summary>
    public IReadOnlyList<string> Permissions { get; set; } = [];

    /// <summary>How many accounts hold it: everyone whose access a change to it affects.</summary>
    public int HolderCount { get; set; }

    /// <summary>Send it back as <c>expectedVersion</c> with a change.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Whether its name, description and permissions can be changed, and it archived (every role but Admin).</summary>
    public bool CanEdit { get; set; }

    /// <summary>Whether it can be deleted: an editable role nobody holds.</summary>
    public bool CanDelete { get; set; }

    /// <summary>Who holds it, by username: only in a single role's response (<c>GET api/roles/{id}</c>).</summary>
    public IReadOnlyList<RoleHolderResponse>? Holders { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastUpdated { get; set; }
}

/// <summary>Someone who holds a role.</summary>
public sealed class RoleHolderResponse
{
    public Guid UserId { get; set; }

    public string Username { get; set; } = string.Empty;
}
