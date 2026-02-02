namespace MedApp.Contracts.Auth.Requests;

public sealed record AssignRoleRequest(
    string Username,
    string Role);
