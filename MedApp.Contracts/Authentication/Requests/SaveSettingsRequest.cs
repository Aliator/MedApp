namespace MedApp.Contracts.Authentication.Requests;

/// <summary>The signed-in user's app settings: a JSON object, saved whole.</summary>
public sealed record SaveSettingsRequest(
    string? Settings);
