namespace MedApp.Domain.Dtos.Requests;

public sealed record AssignRoleRequest(
    string Username,
    string Role);
