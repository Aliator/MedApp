using MedApp.Contracts.Notifications.Responses;
using MediatR;

namespace MedApp.Application.Notifications.Queries.GetMyNotifications;

/// <summary>The user's newest notifications (up to <c>Take</c>, 20 by default and at most 50), and their unread count.</summary>
public sealed record GetMyNotificationsQuery(Guid UserId, int Take = GetMyNotificationsQuery.DefaultTake) : IRequest<NotificationsResponse>
{
    public const int DefaultTake = 20;
    public const int MaxTake = 50;
}
