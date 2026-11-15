using FluentValidation.AspNetCore;
using MedApp.API.Common.Authentication;
using MedApp.API.Notifications;
using MedApp.API.ServerRendering;
using MedApp.Application.Notifications.Common;
using Microsoft.AspNetCore.Authentication;

namespace MedApp.API.Common.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddControllers();

        // Live notifications: pushed to each signed-in user's open pages (NotificationsHub), through a backplane
        // when several copies of the API run.
        services.AddSignalR().AddBackplane(configuration);
        services.AddSingleton<INotificationPublisher, SignalRNotificationPublisher>();

        // Server-side rendering: the Node server that renders the React app's pages (SsrServer), and the client
        // that forwards page requests to it. Redirects (e.g. to sign in) go back to the browser as they are.
        services.AddOptions<SsrOptions>().BindConfiguration(SsrOptions.Section);
        services.AddSingleton<SsrServer>();
        services.AddSingleton<ISsrServer>(provider => provider.GetRequiredService<SsrServer>());
        services.AddHostedService(provider => provider.GetRequiredService<SsrServer>());
        services.AddHttpClient(SsrPages.ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        services.AddFluentValidationAutoValidation();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        // No CORS: the React app is served from the API's own origin in production, and through a same-origin
        // dev proxy in development. The policy for the Blazor client's origin went with it at cutover.

        services.AddSingleton<ISessionCookieService, SessionCookieService>();
        services.AddApiAuthentication();

        return services;
    }

    private static IServiceCollection AddApiAuthentication(
        this IServiceCollection services)
    {
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme =
                    SessionAuthenticationDefaults.Scheme;
                options.DefaultChallengeScheme =
                    SessionAuthenticationDefaults.Scheme;
            })
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(
                SessionAuthenticationDefaults.Scheme,
                _ => { });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthPolicies.Staff, policy => policy.RequireRole("Admin", "User"));

            options.AddPolicy("SelfUserOnly", policy =>
                policy.RequireAssertion(context =>
                {
                    var routeUsername =
                        context.Resource is HttpContext http
                            ? http.Request.RouteValues["username"]?.ToString()
                            : null;

                    var userName = context.User.Identity?.Name;

                    return routeUsername is not null
                           && userName is not null
                           && routeUsername == userName;
                }));
        });

        return services;
    }
}