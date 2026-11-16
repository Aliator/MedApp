using System.Reflection;
using FluentValidation;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Behaviors;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Security;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Patients.Common;
using MedApp.Application.Notifications.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MedApp.Application.Common.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
        });

        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // The clock, so handlers and validators that use "now" can be tested at a fixed time.
        services.TryAddSingleton(TimeProvider.System);

        // Stores a notification and pushes it to its user (the host provides the INotificationPublisher).
        services.AddScoped<INotificationSender, NotificationSender>();

        // Email: links from Email:LinkBaseUrl (the host binds EmailOptions), whether email with links can be sent at
        // all, and the security notices sent after account changes (the host provides the IEmailSender).
        services.AddSingleton<EmailLinks>();
        services.AddSingleton<EmailAvailability>();
        services.AddScoped<ISecurityNotices, SecurityNotices>();

        // Security events (two-factor changes, password resets, email addresses): to the activity log and the
        // application log.
        services.AddScoped<LoggingSecurityEventLog>();
        services.AddScoped<ISecurityEventLog, ActivitySecurityEventLog>();

        // Per-account limits on two-factor codes and email changes: none unless the host provides them (the API does).
        services.TryAddSingleton<IAccountAttemptLimiter, UnlimitedAccountAttempts>();
        // Tells admins about procedure changes that match their monitoring rules.
        services.AddScoped<IProcedureMonitor, ProcedureMonitor>();
        // The activity log (the host provides IActivityContext, the request's actor), and its and the retention
        // schedule's settings, which the host binds to its configuration.
        services.AddScoped<IActivityLog, ActivityLog>();
        services.AddOptions<ActivityOptions>();
        services.AddOptions<RetentionOptions>();

        return services;
    }
}