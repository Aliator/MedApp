using MedApp.Application.Practices.Common;
using FluentValidation.AspNetCore;
using MedApp.API.Common.Authentication;
using MedApp.API.Common.RateLimiting;
using MedApp.API.Fhir;
using MedApp.API.Jobs;
using MedApp.API.Middleware;
using MedApp.API.Notifications;
using MedApp.API.ServerRendering;
using MedApp.Application.Activity.Common;
using MedApp.Application.Jobs.Common;
using MedApp.Application.Notepads.Common;
using MedApp.Application.Specimens.Common;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Patients.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

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

        // Server-side rendering: the Node server that renders the React app's pages (SsrServer: its own container's,
        // at Ssr:ServerUrl, or one the API starts), and the client that forwards page requests to it. Redirects (e.g.
        // to sign in) go back to the browser as they are. No resilience handler (the service defaults add one to every
        // client): a failed render isn't retried, but falls back to the client-rendered app at once, and
        // SsrOptions.RenderTimeout is the only timeout. A Ssr:ServerUrl that isn't a server's address stops startup.
        services.AddOptions<SsrOptions>()
            .BindConfiguration(SsrOptions.Section)
            .Validate(options => options.HasValidServerUrl(), SsrOptions.InvalidServerUrl)
            .ValidateOnStart();
        services.AddSingleton<SsrServer>();
        services.AddSingleton<ISsrServer>(provider => provider.GetRequiredService<SsrServer>());
        services.AddHostedService(provider => provider.GetRequiredService<SsrServer>());

        // Warns at startup outside Development when the data protection keys aren't wrapped.
        services.AddHostedService<MedApp.Infrastructure.Common.Identity.DataProtectionKeyCheck>();
        // RemoveAllResilienceHandlers is still marked experimental (EXTEXP0001); a test checks it does its job.
#pragma warning disable EXTEXP0001
        services.AddHttpClient(SsrPages.ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
            .RemoveAllResilienceHandlers();
        // GOV.UK Notify (patient messages, Live): a retried POST could send a message twice, as Notify has no idempotency
        // key, so its client has no resilience handler; the job decides what's tried again (a timeout never is).
        services.AddHttpClient(MedApp.Infrastructure.PatientMessages.NotifyPatientMessageTransport.ClientName)
            .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001
        services.AddFluentValidationAutoValidation();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        // No CORS: the React app is served from the API's own origin in production, and through a same-origin
        // dev proxy in development. The policy for the Blazor client's origin went with it at cutover.

        services.AddSingleton<ISessionCookieService, SessionCookieService>();
        services.AddApiAuthentication();

        // The activity log's actor is the request's signed-in user and address; its settings, and the retention
        // schedule's periods, come from configuration.
        services.AddHttpContextAccessor();
        // A scheduled job's scope (ScheduledJobScope) acts as the system instead ("medapp-jobs").
        services.AddScoped<HttpActivityContext>();
        services.AddScoped(ActivityContextFor);
        services.AddOptions<ActivityOptions>().BindConfiguration(ActivityOptions.Section);
        services.AddOptions<RetentionOptions>().BindConfiguration(RetentionOptions.Section);
        services.AddOptions<LettersOptions>().BindConfiguration(LettersOptions.Section);
        // Zero or less would clear every notepad as it's read, and refuse every save: stop at startup instead.
        services.AddOptions<NotepadOptions>()
            .BindConfiguration(NotepadOptions.Section)
            .Validate(options => options.IsValid, NotepadOptions.InvalidKeepDays)
            .ValidateOnStart();
        // A specimen number's prefix must fit its column and read as letters: stop at startup otherwise.
        services.AddOptions<SpecimenOptions>()
            .BindConfiguration(SpecimenOptions.Section)
            .Validate(options => options.IsValid, SpecimenOptions.InvalidPrefix)
            .ValidateOnStart();

        // The browser security headers and Content Security Policies (SecurityHeaders), and sign-in throttling.
        services.AddOptions<SecurityHeadersOptions>().BindConfiguration(SecurityHeadersOptions.Section);
        services.AddLoginThrottling();

        return services;
    }

    /// <summary>
    /// Scheduled jobs run inside the API, on a timer (unless <c>Jobs:Enabled</c> is false), one copy at a time each, and
    /// <c>/health</c> is Degraded while one is overdue. Apart from <see cref="AddApi"/>, as it needs the Application and
    /// Infrastructure layers (the runner, the jobs and their repository); <c>Program</c> adds it after them.
    /// </summary>
    public static IServiceCollection AddScheduledJobHost(this IServiceCollection services)
    {
        services.AddHostedService<ScheduledJobHost>();
        services.AddHealthChecks().AddCheck<ScheduledJobsHealthCheck>(ScheduledJobsHealthCheck.Name);
        return services;
    }

    /// <summary>The activity log's actor in a scope: the system in a scheduled job's run, otherwise the request's user.</summary>
    public static IActivityContext ActivityContextFor(IServiceProvider provider) =>
        provider.GetRequiredService<ScheduledJobScope>().IsJob
            ? SystemActivityContext.Instance
            : provider.GetRequiredService<HttpActivityContext>();

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

        // A refused FHIR request gets an OperationOutcome; a missing permission a 403 that names it; anything else as before.
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationResultHandler>();

        // One policy per permission (RequirePermission), checked against the session's permission claims.
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicies>();
        services.AddSingleton<IAuthorizationHandler, PermissionRequirementHandler>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthPolicies.SelfUserOnly, policy =>
                policy.RequireAssertion(context =>
                    context.Resource is HttpContext http && AuthPolicies.IsOwnUsername(http, context.User)));
        });

        return services;
    }
}