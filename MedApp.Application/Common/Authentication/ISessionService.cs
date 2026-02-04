namespace MedApp.Application.Common.Authentication;

public interface ISessionService
{
    Task<LoginResult> CreateSessionAsync(
        Guid userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default);

    Task<SessionValidationResult?> ValidateSessionAsync(
        Guid sessionId,
        CancellationToken ct = default);

    Task RevokeSessionAsync(
        Guid sessionId,
        CancellationToken ct = default);
}