using System.Reflection;
using FluentValidation;
using MedApp.Application.Common.Behaviors;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Security;
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

        // Security events (two-factor changes, password resets): to the application log until the activity log
        // replaces this registration (ISecurityEventLog).
        services.TryAddScoped<ISecurityEventLog, LoggingSecurityEventLog>();

        return services;
    }
}