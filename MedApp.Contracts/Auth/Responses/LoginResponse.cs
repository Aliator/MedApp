namespace MedApp.Contracts.Auth.Responses;

public sealed record LoginResponse(
    Guid SessionId,
    DateTime ExpiresAtUtc
);