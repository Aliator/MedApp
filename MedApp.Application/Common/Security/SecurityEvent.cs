namespace MedApp.Application.Common.Security;

/// <summary>
/// Something that happened to an account's security. <paramref name="UserId"/> and <paramref name="Username"/> are the
/// account it's about (null for a reset request that matched no account); <paramref name="ActorUserId"/> and
/// <paramref name="ActorUsername"/> are who did it, when that's someone else (an admin). <paramref name="Details"/> are
/// small facts (a count, on or off), never an email address, a password, a token or a code.
/// </summary>
public sealed record SecurityEvent(
    SecurityEventKind Kind,
    Guid? UserId,
    string? Username,
    Guid? ActorUserId = null,
    string? ActorUsername = null,
    IReadOnlyDictionary<string, object?>? Details = null);
