using MedApp.API.Common.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MedApp.API.Notifications;

/// <summary>
/// The live connection each signed-in page keeps open, which new notifications are pushed down ("notification",
/// with a <c>NotificationResponse</c>). It's addressed by user (the session's user ID), so every page someone has
/// open gets theirs. Pages only listen; there's nothing to call.
/// </summary>
[Authorize(Policy = AuthPolicies.Staff)]
public sealed class NotificationsHub : Hub
{
    /// <summary>Where the hub is mapped.</summary>
    public const string Path = "/api/notifications/hub";

    /// <summary>The client method a new notification arrives on.</summary>
    public const string NotificationMethod = "notification";
}
