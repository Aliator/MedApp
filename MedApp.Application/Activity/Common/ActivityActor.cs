namespace MedApp.Application.Activity.Common;

/// <summary>
/// Who an activity log entry is about: the signed-in user (or the person signing in), and the address their request
/// came from. <paramref name="UserId"/> is null only for a refused sign-in to an account that doesn't exist.
/// </summary>
public sealed record ActivityActor(Guid? UserId, string Username, string? IpAddress);
