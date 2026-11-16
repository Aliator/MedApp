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

    /// <summary>
    /// Ends every session of the user that's still active, except <paramref name="exceptSessionId"/> if it's given
    /// (the session making a change to its own account). Returns how many it ended.
    /// </summary>
    Task<int> RevokeUserSessionsAsync(
        Guid userId,
        Guid? exceptSessionId = null,
        CancellationToken ct = default);
}