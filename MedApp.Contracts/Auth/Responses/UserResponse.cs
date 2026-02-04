namespace MedApp.Contracts.Auth.Responses;

public sealed record UserResponse(
    string Username,
    IReadOnlyList<string> Roles
);