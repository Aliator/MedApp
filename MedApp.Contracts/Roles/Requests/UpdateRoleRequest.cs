namespace MedApp.Contracts.Roles.Requests;

/// <summary>A role's name, description and exactly its permissions, from the version the caller saw.</summary>
public sealed class UpdateRoleRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Exactly the permissions it should grant, by name: any it has that aren't listed are removed.</summary>
    public IReadOnlyList<string>? Permissions { get; set; }

    /// <summary>The <c>version</c> the caller saw: if another admin has changed the role since, it's a 409.</summary>
    public string ExpectedVersion { get; set; } = string.Empty;
}
