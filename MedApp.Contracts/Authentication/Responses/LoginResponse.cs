namespace MedApp.Contracts.Authentication.Responses;

public sealed record LoginResponse(
    Guid SessionId,
    DateTime ExpiresAtUtc
);