using MedApp.Application.Activity.Repositories;
using MedApp.Application.Authentication.Settings.Repositories;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Application.Demographics.Common;
using MedApp.Application.Notifications.Repositories;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.Infrastructure.Common.Authentication;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data;
using MedApp.Infrastructure.Demographics;
using MedApp.Infrastructure.Data.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedApp.Infrastructure.Common.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDatabase(configuration);
        services.AddIdentity(configuration);
        services.AddRepositories();
        services.AddDemographics(configuration);

        return services;
    }

    private static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Retries what SQL Server reports as transient: Azure SQL serverless waking from a pause (about a minute),
        // failovers and dropped connections. A transaction the app starts itself must run inside the execution
        // strategy (CreateExecutionStrategy), as IdentityTransactions does, or EF refuses it.
        services.AddDbContext<MedAppDbContext>(options => options
            .UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null))
            .ConfigureWarnings(LogFailedSavesAtDebug));

        return services;
    }

    /// <summary>
    /// A failed save's or command's log carries SQL Server's message, which can hold a value: a duplicate NHS number,
    /// from the unique index. So EF Core logs those at Debug, which the app doesn't write. A failure that isn't handled
    /// still reaches the log, as the server's unhandled exception; a handled one, like the NHS number's 409, logs no
    /// patient data. The events: <c>Microsoft.EntityFrameworkCore.Update</c>'s failed save, and
    /// <c>Microsoft.EntityFrameworkCore.Database.Command</c>'s failed command.
    /// </summary>
    public static void LogFailedSavesAtDebug(WarningsConfigurationBuilder warnings) =>
        warnings.Log(
            (CoreEventId.SaveChangesFailed, LogLevel.Debug),
            (RelationalEventId.CommandError, LogLevel.Debug));

    private static IServiceCollection AddRepositories(
        this IServiceCollection services)
    {
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IPatientProcedureRepository, PatientProcedureRepository>();
        services.AddScoped<IPatientProcedureStagesRepository, PatientProcedureStagesRepository>();
        services.AddScoped<IProcedureAuditRepository, ProcedureAuditRepository>();
        services.AddScoped<IUserSettingsRepository, UserSettingsRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<IPatientRecordRepository, PatientRecordRepository>();
        
        return services;
    }

    /// <summary>
    /// The demographics lookup (<c>Pds:Mode</c>): none by default, or the offline stub of synthetic 999 numbers. The
    /// real PDS FHIR API isn't wired in; it needs NHS England's onboarding first.
    /// </summary>
    private static IServiceCollection AddDemographics(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(PdsOptions.Section).Get<PdsOptions>() ?? new PdsOptions();
        if (options.Mode == PdsMode.Offline)
            services.AddSingleton<IPersonalDemographicsService, OfflinePersonalDemographicsService>();
        else
            services.AddSingleton<IPersonalDemographicsService, NoPersonalDemographicsService>();

        return services;
    }

    private static IServiceCollection AddIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<IdentitySettings>(
            configuration.GetSection("Identity"));

        services.AddSingleton<IConfigureOptions<IdentityOptions>, IdentityOptionsConfiguration>();

        services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<MedAppDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IAuthenticationService, IdentityAuthenticationService>();
        services.AddScoped<ISessionService, SessionService>();

        services.AddScoped<IIdentityReadService, IdentityReadService>();
        services.AddScoped<IIdentityUserService, IdentityUserService>();
        services.AddScoped<IIdentityRoleService, IdentityRoleService>();
        services.AddScoped<IIdentityTransactions, IdentityTransactions>();

        services.AddScoped<IdentityUserService>();
        services.AddScoped<IdentityRoleService>();
        services.AddScoped<FirstAdminSetup>();

        return services;
    }
}