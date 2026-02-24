namespace MedApp.Contracts.Authentication.Requests;

public sealed record AssignRoleRequest(
    string Username,
    string Role);
