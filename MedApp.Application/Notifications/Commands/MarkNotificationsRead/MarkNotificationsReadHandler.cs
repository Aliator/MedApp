using MedApp.Application.Notifications.Repositories;
using MediatR;

namespace MedApp.Application.Notifications.Commands.MarkNotificationsRead;

public sealed class MarkNotificationsReadHandler(INotificationRepository repository, TimeProvider timeProvider)
    : IRequestHandler<MarkNotificationsReadCommand, int>
{
    public Task<int> Handle(MarkNotificationsReadCommand request, CancellationToken cancellationToken) =>
        repository.MarkReadAsync(
            request.UserId,
            request.NotificationId,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
}
