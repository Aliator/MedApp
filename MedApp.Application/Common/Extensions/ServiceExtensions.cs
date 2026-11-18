using System.Reflection;
using FluentValidation;
using MedApp.Application.Activity.Common;
using MedApp.Application.Authentication.TwoFactor.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Behaviors;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Security;
using MedApp.Application.Jobs.Common;
using MedApp.Application.Jobs.OperationalPurge;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Notepads.Common;
using MedApp.Application.Specimens.Common;
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

        // The working day's rules with the closed days looked up, for the calendar's validators.
        services.AddScoped<WorkingDays>();

        // Stores a notification and pushes it to its user (the host provides the INotificationPublisher).
        services.AddScoped<INotificationSender, NotificationSender>();

        // Email: links from Email:LinkBaseUrl (the host binds EmailOptions), whether email with links can be sent at
        // all, and the security notices sent after account changes (the host provides the IEmailSender).
        services.AddSingleton<EmailLinks>();
        services.AddSingleton<EmailAvailability>();
        services.AddScoped<ISecurityNotices, SecurityNotices>();
        services.AddScoped<IEmailCodeService, EmailCodeService>();

        // Security events (two-factor changes, password resets, email addresses): to the activity log and the
        // application log.
        services.AddScoped<LoggingSecurityEventLog>();
        services.AddScoped<ISecurityEventLog, ActivitySecurityEventLog>();

        // Per-account limits on two-factor codes and email changes: none unless the host provides them (the API does).
        services.TryAddSingleton<IAccountAttemptLimiter, UnlimitedAccountAttempts>();
        // Tells admins about pathway changes that match their monitoring rules.
        services.AddScoped<IPathwayMonitor, PathwayMonitor>();
        // The activity log (the host provides IActivityContext, the request's actor), and its and the retention
        // schedule's settings, which the host binds to its configuration.
        services.AddScoped<IActivityLog, ActivityLog>();
        services.AddOptions<ActivityOptions>();
        services.AddOptions<RetentionOptions>();
        // How long a notepad is kept without a change (the host binds it, the Notepad section).
        services.AddOptions<NotepadOptions>();
        // Specimen numbers' prefix (the host binds it, the Specimens section).
        services.AddOptions<SpecimenOptions>();

        // Scheduled jobs: the runner (the host decides whether to run it on a timer), each job (scoped: a scope per
        // run), and their settings, which the host binds (Jobs, Retention:Operational).
        services.AddSingleton<IScheduledJobRunner, ScheduledJobRunner>();
        services.AddScoped<ScheduledJobScope>();
        services.AddScoped<IScheduledJob, OperationalPurgeJob>();
        services.AddOptions<JobsOptions>();
        services.AddOptions<OperationalRetentionOptions>();

        return services;
    }
}