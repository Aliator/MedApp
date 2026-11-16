using MedApp.Contracts.Notifications.Responses;
using MediatR;

namespace MedApp.Application.Notifications.Queries.GetMyNotificationPreferences;

/// <summary>Which notifications the signed-in user gets.</summary>
public sealed record GetMyNotificationPreferencesQuery(Guid UserId) : IRequest<NotificationPreferencesResponse>;
