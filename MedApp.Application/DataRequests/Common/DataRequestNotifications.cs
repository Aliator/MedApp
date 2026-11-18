using MedApp.Domain.DataRequests;
using MedApp.Domain.Notifications;

namespace MedApp.Application.DataRequests.Common;

/// <summary>
/// "Data request due soon" (<see cref="NotificationKind.DataRequestDueSoon"/>): a fixed title, and a message with the due
/// date only, never the patient or who asked; the request is its <see cref="Notification.SubjectId"/>.
/// </summary>
public static class DataRequestNotifications
{
    public const string DueSoonTitle = "Data request due soon";

    public static IReadOnlyList<Notification> DueSoon(IEnumerable<Guid> userIds, DataRequest request, DateTime nowUtc)
    {
        var message = $"A data request is due on {Words.Date(request.DueOn)}. Open it to check where it stands.";
        return userIds
            .Distinct()
            .Select(userId => new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Kind = NotificationKind.DataRequestDueSoon,
                Title = DueSoonTitle,
                Message = message,
                SubjectId = request.Id,
                CreatedAtUtc = nowUtc
            })
            .ToList();
    }
}
