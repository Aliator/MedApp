namespace MedApp.Contracts.Notifications.Responses;

/// <summary>The signed-in user's newest notifications, and how many they haven't read (of all of them).</summary>
public sealed record NotificationsResponse(IReadOnlyList<NotificationResponse> Items, int UnreadCount);
