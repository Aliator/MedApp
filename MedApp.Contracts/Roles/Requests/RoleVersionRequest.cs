namespace MedApp.Contracts.Roles.Requests;

/// <summary>Archiving or restoring a role, from the version the caller saw.</summary>
public sealed class RoleVersionRequest
{
    /// <summary>The <c>version</c> the caller saw: if another admin has changed the role since, it's a 409.</summary>
    public string ExpectedVersion { get; set; } = string.Empty;
}
