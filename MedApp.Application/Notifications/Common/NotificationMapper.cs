using MedApp.Contracts.Notifications.Responses;
using MedApp.Domain.Notifications;

namespace MedApp.Application.Notifications.Common;

public static class NotificationMapper
{
    /// <summary>A user's newest notifications and how many they haven't read.</summary>
    public static NotificationsResponse ToResponse(IEnumerable<Notification> notifications, int unreadCount) =>
        new(notifications.Select(ToResponse).ToList(), unreadCount);

    public static NotificationResponse ToResponse(Notification notification) => new(
        notification.Id,
        notification.Kind.ToString(),
        notification.Title,
        notification.Message,
        notification.PathwayId,
        notification.CreatedAtUtc,
        notification.ReadAtUtc is not null,
        notification.AppointmentId,
        notification.CalendarDate,
        notification.NoteDeletionRequestId,
        notification.MeetingId,
        notification.ReferralId);

    /// <summary>A user's notification choices: their own, or the defaults (everything) when they've never chosen.</summary>
    public static NotificationPreferencesResponse ToResponse(NotificationPreferences preferences) =>
        new(preferences.PathwayAssigned);
}
