using MedApp.Application.Notifications.Common;
using MedApp.Application.Notifications.Repositories;
using MedApp.Contracts.Notifications.Responses;
using MediatR;

namespace MedApp.Application.Notifications.Queries.GetMyNotifications;

public sealed class GetMyNotificationsHandler(INotificationRepository repository)
    : IRequestHandler<GetMyNotificationsQuery, NotificationsResponse>
{
    public async Task<NotificationsResponse> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        var take = Math.Clamp(request.Take, 1, GetMyNotificationsQuery.MaxTake);
        var notifications = await repository.GetRecentAsync(request.UserId, take, cancellationToken);
        var unread = await repository.CountUnreadAsync(request.UserId, cancellationToken);

        return new NotificationsResponse(notifications.Select(NotificationMapper.ToResponse).ToList(), unread);
    }
}
