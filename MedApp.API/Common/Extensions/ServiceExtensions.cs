using FluentValidation.AspNetCore;
using MedApp.API.Common.Authentication;
using MedApp.API.Common.RateLimiting;
using MedApp.API.Middleware;
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
        // that forwards page requests to it. Redirects (e.g. to sign in) go back to the browser as they are. No
        // resilience handler (the service defaults add one to every client): a failed render isn't retried, but falls
        // back to the client-rendered app at once, and SsrOptions.RenderTimeout is the only timeout.
        services.AddOptions<SsrOptions>().BindConfiguration(SsrOptions.Section);
        services.AddSingleton<SsrServer>();
        services.AddSingleton<ISsrServer>(provider => provider.GetRequiredService<SsrServer>());
        services.AddHostedService(provider => provider.GetRequiredService<SsrServer>());
        // RemoveAllResilienceHandlers is still marked experimental (EXTEXP0001); a test checks it does its job.
#pragma warning disable EXTEXP0001
        services.AddHttpClient(SsrPages.ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
            .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001
        services.AddFluentValidationAutoValidation();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        // No CORS: the React app is served from the API's own origin in production, and through a same-origin
        // dev proxy in development. The policy for the Blazor client's origin went with it at cutover.

        services.AddSingleton<ISessionCookieService, SessionCookieService>();
        services.AddApiAuthentication();

        // The browser security headers and Content Security Policies (SecurityHeaders), and sign-in throttling.
        services.AddOptions<SecurityHeadersOptions>().BindConfiguration(SecurityHeadersOptions.Section);
        services.AddLoginThrottling();

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

            options.AddPolicy(AuthPolicies.SelfUserOnly, policy =>
                policy.RequireAssertion(context =>
                    context.Resource is HttpContext http && AuthPolicies.IsOwnUsername(http, context.User)));
        });

        return services;
    }
}