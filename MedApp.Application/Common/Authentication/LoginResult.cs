namespace MedApp.Application.Common.Authentication;

public sealed record LoginResult(
    Guid SessionId,
    DateTime ExpiresAtUtc
);