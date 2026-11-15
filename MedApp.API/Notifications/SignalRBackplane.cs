using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace MedApp.API.Notifications;

/// <summary>
/// Lets several copies of the API share live notifications. A browser's SignalR connection is to one copy, but the
/// change that notifies its user may be handled by another, so every copy passes its messages through a shared
/// Redis-compatible server (Valkey in Kubernetes: deploy/helm/medapp). Without the connection string, as in
/// development, there's one copy and no backplane.
/// </summary>
public static class SignalRBackplane
{
    /// <summary>The connection string's name: <c>ConnectionStrings:SignalRBackplane</c>.</summary>
    public const string ConnectionName = "SignalRBackplane";

    /// <summary>The prefix on the backplane's channels, so it can share a server with anything else.</summary>
    public const string ChannelPrefix = "MedApp";

    public static ISignalRServerBuilder AddBackplane(this ISignalRServerBuilder signalR, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
            return signalR;

        return signalR.AddStackExchangeRedis(connectionString, options =>
        {
            options.Configuration.ChannelPrefix = RedisChannel.Literal(ChannelPrefix);
            // Start even if the backplane isn't up yet, and keep reconnecting; meanwhile notifications still reach
            // pages connected to the copy that sends them, and every page on its next load.
            options.Configuration.AbortOnConnectFail = false;
        });
    }
}
