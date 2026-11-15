namespace MedApp.Application.Common.Authentication;

public sealed record SessionValidationResult(
    Guid UserId,
    string Username,
    IReadOnlyCollection<string> Roles
);