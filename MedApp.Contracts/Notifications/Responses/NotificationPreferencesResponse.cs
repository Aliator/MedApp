namespace MedApp.Contracts.Notifications.Responses;

/// <summary>Which notifications the signed-in user gets (every kind, until they choose).</summary>
public sealed record NotificationPreferencesResponse(bool PathwayAssigned);
