namespace MedApp.Contracts.Auth.Requests;

public sealed record RevokeRoleRequest(
    string Username,
    string Role);