namespace MedApp.Application.Common.Authentication;

public sealed record SessionToken(
    Guid SessionId,
    DateTime ExpiresAtUtc
);