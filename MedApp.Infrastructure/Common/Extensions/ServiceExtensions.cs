using Azure.Storage.Blobs;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Attachments.Repositories;
using MedApp.Application.Authentication.Settings.Repositories;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Repositories;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.Infrastructure.Attachments;
using MedApp.Infrastructure.Common.Authentication;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data;
using MedApp.Infrastructure.Data.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddAttachmentStorage(configuration);

        return services;
    }

    /// <summary>
    /// Attached files: the blob store (or none, when <c>ConnectionStrings:Attachments</c> isn't set: uploads and
    /// downloads are then refused, and nothing else changes), the malware scanner (none yet), thumbnails and the
    /// temporary files uploads are checked in.
    /// </summary>
    private static IServiceCollection AddAttachmentStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AttachmentStorageOptions>().BindConfiguration(AttachmentStorageOptions.Section);

        var connectionString = configuration.GetConnectionString(AttachmentStorageOptions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddSingleton<IFileStore, UnconfiguredFileStore>();
        }
        else
        {
            services.AddSingleton(provider => new BlobServiceClient(AttachmentStorageOptions.WithoutContainerName(connectionString))
                .GetBlobContainerClient(provider.GetRequiredService<IOptions<AttachmentStorageOptions>>().Value.Container));
            services.AddSingleton<IFileStore, BlobFileStore>();
        }

        services.AddSingleton<IMalwareScanner, NoMalwareScanner>();
        services.AddSingleton<IThumbnailGenerator, SkiaThumbnailGenerator>();
        services.AddSingleton<IUploadStaging, TempFileUploadStaging>();

        return services;
    }

    private static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Retries what SQL Server reports as transient: Azure SQL serverless waking from a pause (about a minute),
        // failovers and dropped connections. A transaction the app starts itself must run inside the execution
        // strategy (CreateExecutionStrategy), as IdentityTransactions does, or EF refuses it.
        services.AddDbContext<MedAppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null)));

        return services;
    }

    private static IServiceCollection AddRepositories(
        this IServiceCollection services)
    {
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IPatientProcedureRepository, PatientProcedureRepository>();
        services.AddScoped<IPatientProcedureStagesRepository, PatientProcedureStagesRepository>();
        services.AddScoped<IProcedureAuditRepository, ProcedureAuditRepository>();
        services.AddScoped<IProcedureInsightsRepository, ProcedureInsightsRepository>();
        services.AddScoped<IUserSettingsRepository, UserSettingsRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();
        
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