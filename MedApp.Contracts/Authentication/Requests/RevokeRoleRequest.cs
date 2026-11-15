namespace MedApp.Contracts.Authentication.Requests;

public sealed record RevokeRoleRequest(
    string Username,
    string Role);