namespace MedApp.Contracts.Auth.Responses;

public sealed record WhoAmIResponse(
    bool IsAuthenticated,
    string? Name,
    IReadOnlyList<string> Roles
);