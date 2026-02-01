namespace MedApp.API.Dtos.Requests;

public sealed record AssignRoleRequest(
    string Username,
    string Role);
