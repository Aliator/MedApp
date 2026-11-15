using MediatR;

namespace MedApp.Application.Notifications.Commands.MarkNotificationsRead;

/// <summary>Marks one of the user's notifications read, or all of them (no <c>NotificationId</c>).</summary>
public sealed record MarkNotificationsReadCommand(Guid UserId, Guid? NotificationId) : IRequest<int>;
