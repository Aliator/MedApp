namespace MedApp.Contracts.Authentication.Responses;

/// <summary>The signed-in user's app settings (a JSON object), or null for none saved yet.</summary>
public sealed record UserSettingsResponse(
    string? Settings,
    DateTime? UpdatedAtUtc);
