using MedApp.Contracts.Notifications.Responses;
using MedApp.Domain.Notifications;

namespace MedApp.Application.Notifications.Common;

public static class NotificationMapper
{
    public static NotificationResponse ToResponse(Notification notification) => new(
        notification.Id,
        notification.Kind.ToString(),
        notification.Title,
        notification.Message,
        notification.ProcedureId,
        notification.CreatedAtUtc,
        notification.ReadAtUtc is not null);
}
