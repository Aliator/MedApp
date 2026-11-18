using MedApp.Application.Notifications.Common;
using MedApp.Application.Notifications.Repositories;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Domain.Notifications;
using MediatR;

namespace MedApp.Application.Notifications.Commands.UpdateMyNotificationPreferences;

public sealed class UpdateMyNotificationPreferencesHandler(INotificationPreferencesRepository repository, TimeProvider timeProvider)
    : IRequestHandler<UpdateMyNotificationPreferencesCommand, NotificationPreferencesResponse>
{
    public async Task<NotificationPreferencesResponse> Handle(UpdateMyNotificationPreferencesCommand request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await repository.SaveAsync(request.UserId, request.PathwayAssigned, now, cancellationToken);

        return NotificationMapper.ToResponse(new NotificationPreferences
        {
            UserId = request.UserId,
            PathwayAssigned = request.PathwayAssigned,
            UpdatedAtUtc = now
        });
    }
}
