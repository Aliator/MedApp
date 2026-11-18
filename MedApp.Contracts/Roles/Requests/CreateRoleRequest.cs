namespace MedApp.Contracts.Roles.Requests;

/// <summary>A new role: its name (unique), what it's for, and the permissions it grants, by name.</summary>
public sealed class CreateRoleRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Permission names (<c>GET api/permissions</c>). Every role also grants <c>UseApp</c>. The Administrator-only ones
    /// are refused (400).
    /// </summary>
    public IReadOnlyList<string>? Permissions { get; set; }
}
