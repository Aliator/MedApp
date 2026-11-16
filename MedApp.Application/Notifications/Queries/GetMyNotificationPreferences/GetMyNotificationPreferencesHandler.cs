using MedApp.Application.Notifications.Common;
using MedApp.Application.Notifications.Repositories;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Domain.Notifications;
using MediatR;

namespace MedApp.Application.Notifications.Queries.GetMyNotificationPreferences;

public sealed class GetMyNotificationPreferencesHandler(INotificationPreferencesRepository repository)
    : IRequestHandler<GetMyNotificationPreferencesQuery, NotificationPreferencesResponse>
{
    public async Task<NotificationPreferencesResponse> Handle(GetMyNotificationPreferencesQuery request, CancellationToken cancellationToken)
    {
        var preferences = await repository.GetAsync(request.UserId, cancellationToken)
                          ?? NotificationPreferences.Defaults(request.UserId);
        return NotificationMapper.ToResponse(preferences);
    }
}
