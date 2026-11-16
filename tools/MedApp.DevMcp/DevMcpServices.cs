using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Extensions;
using MedApp.Application.Notifications.Common;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Infrastructure.Common.Extensions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.DevMcp;

/// <summary>
/// The app's services as this tool uses them: the Application and Infrastructure layers, plus what the API's web host
/// would otherwise provide. A test builds these with validation on, so a service the layers need but the tool lacks
/// fails there rather than when the tool starts.
/// </summary>
public static class DevMcpServices
{
    public static IServiceCollection AddDevMcpCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDataProtection(); // the API's web host provides this; Identity's token providers need it
        services.AddApplication();
        services.AddInfrastructure(configuration);

        // The activity log's actor: this tool, with no user and no address (the API's comes from the request).
        services.AddSingleton<IActivityContext, DevToolActivityContext>();

        // No live notifications: nobody is connected to this tool. They're still stored, and show on the next load.
        services.AddSingleton<INotificationPublisher, NoLiveNotifications>();
        return services;
    }
}

/// <summary>The activity log's actor for changes this tool makes.</summary>
public sealed class DevToolActivityContext : IActivityContext
{
    public const string Username = "medapp-dev";

    public ActivityActor Actor { get; } = new(null, Username, null);
}

/// <summary>Pushes nothing: the tool has no SignalR hub, and nobody to push to.</summary>
public sealed class NoLiveNotifications : INotificationPublisher
{
    public Task PublishAsync(Guid userId, NotificationResponse notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
