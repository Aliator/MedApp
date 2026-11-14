namespace MedApp.Contracts.Authentication.Requests;

/// <summary>The roles a user should have, exactly: any others they have are removed. Empty removes them all.</summary>
public sealed record SetUserRolesRequest(
    IReadOnlyList<string>? Roles);
