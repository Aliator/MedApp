namespace MedApp.Contracts.Authentication.Responses;

public sealed record UserResponse(
    string Username,
    IReadOnlyList<string> Roles
);