using MedApp.Domain.Notifications;

namespace MedApp.UnitTests.Common.Constants;

public static class NotificationsTestConstants
{
    public static readonly Guid UserId = Guid.Parse("0000000b-0000-0000-0000-000000000001");
    public static readonly Guid NotificationId = Guid.Parse("0000000b-0000-0000-0000-000000000002");
    public static readonly Guid PathwayId = Guid.Parse("0000000b-0000-0000-0000-000000000003");
    public static readonly Guid AppointmentId = Guid.Parse("0000000b-0000-0000-0000-000000000004");
    public static readonly DateTime CreatedAtUtc = new(2026, 6, 15, 9, 30, 0, DateTimeKind.Utc);
    public static readonly DateTime Now = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    public const string Title = "Test pathway";
    public const string Message = "test.admin assigned you to this pathway.";

    public static Notification CreateNotification(DateTime? readAtUtc = null) => new()
    {
        Id = NotificationId,
        UserId = UserId,
        Kind = NotificationKind.PathwayAssigned,
        Title = Title,
        Message = Message,
        PathwayId = PathwayId,
        CreatedAtUtc = CreatedAtUtc,
        ReadAtUtc = readAtUtc
    };
}
