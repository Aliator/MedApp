namespace MedApp.Contracts.Authentication.Responses;

public sealed record WhoAmIResponse(
    bool IsAuthenticated,
    string? Name,
    IReadOnlyList<string> Roles
);