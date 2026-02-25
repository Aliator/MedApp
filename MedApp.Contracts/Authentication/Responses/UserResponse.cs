namespace MedApp.Contracts.Authentication.Responses;

public sealed record UserResponse(
    Guid UserId,
    string Username,
    IReadOnlyList<string> Roles
);