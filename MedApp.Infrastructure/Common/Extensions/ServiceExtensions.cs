using MedApp.Application.Practices.Repositories;
using Azure.Identity;
using Azure.Storage.Blobs;
using MedApp.Application.Activity.Repositories;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Attachments.Repositories;
using MedApp.Application.Authentication.Roles.Repositories;
using MedApp.Application.Authentication.Security.Repositories;
using MedApp.Application.Authentication.Settings.Repositories;
using MedApp.Application.Authentication.TwoFactor.Repositories;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Clinics.Repositories;
using MedApp.Application.Sites.Repositories;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Identity;
using MedApp.Application.Demographics.Common;
using MedApp.Application.Jobs.Common;
using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Repositories;
using MedApp.Application.Jobs.OperationalPurge;
using MedApp.Application.Jobs.Repositories;
using MedApp.Application.Monitoring.Repositories;
using MedApp.Application.Notepads.Repositories;
using MedApp.Application.Notifications.Repositories;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.PatientMessages.Common;
using MedApp.Application.PatientMessages.Repositories;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Referrals.Repositories;
using MedApp.Application.Specimens.Repositories;
using MedApp.Application.Specialties.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Infrastructure.Attachments;
using MedApp.Infrastructure.Common.Authentication;
using MedApp.Infrastructure.Common.Email;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data;
using MedApp.Infrastructure.Demographics;
using MedApp.Infrastructure.Letters;
using MedApp.Infrastructure.PatientMessages;
using MedApp.Infrastructure.Data.Repositories;
using Microsoft.AspNetCore.DataProtection;
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
        services.AddDataProtectionKeys(configuration);
        services.AddIdentity(configuration);
        services.AddEmail();
        services.AddRepositories();
        services.AddAttachmentStorage(configuration);
        services.AddDemographics(configuration);
        services.AddJobSettings(configuration);
        services.AddPatientMessages(configuration);

        return services;
    }

    /// <summary>
    /// Texts and emails to patients (centre plan decision 128): their settings (<c>PatientMessages</c>, <c>Reminders</c>)
    /// and the sender <c>PatientMessages:Mode</c> chooses: none (Off, the default), the offline stand-in that emails
    /// Mailpit, or GOV.UK Notify (Live), whose key and templates must be set or the host stops at startup.
    /// </summary>
    private static IServiceCollection AddPatientMessages(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PatientMessageOptions>()
            .BindConfiguration(PatientMessageOptions.Section)
            .Validate(options => options.IsValid, PatientMessageOptions.UnsafeOfflineRecipient)
            .ValidateOnStart();
        services.AddOptions<ReminderOptions>()
            .BindConfiguration(ReminderOptions.Section)
            .Validate(options => options.IsValid, ReminderOptions.InvalidSettings)
            .ValidateOnStart();

        var mode = configuration.GetSection(PatientMessageOptions.Section).Get<PatientMessageOptions>()?.Mode ?? PatientMessageMode.Off;
        switch (mode)
        {
            case PatientMessageMode.Offline:
                services.AddSingleton<IPatientMessageTransport, OfflinePatientMessageTransport>();
                break;
            case PatientMessageMode.Live:
                services.AddOptions<NotifyOptions>()
                    .BindConfiguration(NotifyOptions.Section)
                    .Validate(options => options.IsComplete, NotifyOptions.Incomplete)
                    .ValidateOnStart();
                // A named client: the API takes the default resilience handler off it (NotifyPatientMessageTransport.ClientName),
                // as a retried POST could send a message twice (Notify has no idempotency key).
                services.AddHttpClient(NotifyPatientMessageTransport.ClientName, (provider, client) =>
                    client.Timeout = TimeSpan.FromSeconds(Math.Max(1, provider.GetRequiredService<IOptions<NotifyOptions>>().Value.TimeoutSeconds)));
                services.AddSingleton<IPatientMessageTransport, NotifyPatientMessageTransport>();
                break;
            default:
                services.AddSingleton<IPatientMessageTransport, NoPatientMessageTransport>();
                break;
        }

        // Letters' PDFs (PDFsharp and MigraDoc, with the bundled font): stateless, with its own limit per copy.
        services.AddSingleton<ILetterRenderer, PdfSharpLetterRenderer>();
        // Letters by secure email, straight to the relay (Letters:SecureEmail), off until it's set up and declared.
        services.AddSingleton<ILetterTransport, SecureEmailLetterTransport>();

        return services;
    }

    /// <summary>
    /// The scheduled jobs' settings: <c>Jobs:Enabled</c>, each job's <c>Jobs:{Name}:Enabled</c>, and the operational
    /// purge's periods (<c>Retention:Operational</c>), with periods under 1 stopping a host at startup. Whether a timer
    /// runs the jobs is the host's choice (the API's <c>ScheduledJobHost</c>).
    /// </summary>
    private static IServiceCollection AddJobSettings(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JobsOptions>().Configure(options => BindJobs(configuration.GetSection(JobsOptions.Section), options));
        services.AddOptions<OperationalRetentionOptions>()
            .BindConfiguration(OperationalRetentionOptions.Section)
            .Validate(options => options.IsValid, OperationalRetentionOptions.InvalidPeriods)
            .ValidateOnStart();

        return services;
    }

    /// <summary>Reads <c>Jobs:Enabled</c>, and each child section's <c>Enabled</c> as that job's switch.</summary>
    public static void BindJobs(IConfigurationSection section, JobsOptions options)
    {
        if (bool.TryParse(section["Enabled"], out var enabled))
            options.Enabled = enabled;

        foreach (var job in section.GetChildren())
        {
            if (bool.TryParse(job["Enabled"], out var jobEnabled))
                options.JobEnabled[job.Key] = jobEnabled;
        }
    }

    /// <summary>The setting that turns on Key Vault protection of the data protection keys (off when empty).</summary>
    public const string DataProtectionKeyVaultKeySetting = "DataProtection:KeyVaultKeyUri";

    /// <summary>
    /// ASP.NET Core's data protection keys, which seal reset and confirmation tokens, kept in the database
    /// (<c>DataProtectionKeys</c>), so every copy of the API shares them: a link one copy makes opens on any other, and
    /// survives a restart. On Azure, <see cref="DataProtectionKeyVaultKeySetting"/> names a Key Vault key that wraps
    /// them, so a copy of the database alone can't unseal them; locally it's unset, and they're stored unwrapped.
    /// </summary>
    private static IServiceCollection AddDataProtectionKeys(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dataProtection = services.AddDataProtection()
            .SetApplicationName("MedApp")
            .PersistKeysToDbContext<MedAppDbContext>();

        if (configuration[DataProtectionKeyVaultKeySetting] is { Length: > 0 } keyUri)
            dataProtection.ProtectKeysWithAzureKeyVault(new Uri(keyUri), new DefaultAzureCredential());

        return services;
    }

    /// <summary>
    /// Email: queued in memory (<see cref="EmailQueue"/>, the app's <see cref="IEmailSender"/>) and sent over SMTP in the
    /// background (<see cref="EmailDispatcher"/>), from the "Email" settings. Without a mail server nothing is sent.
    /// </summary>
    private static IServiceCollection AddEmail(this IServiceCollection services)
    {
        services.AddOptions<EmailOptions>().BindConfiguration(EmailOptions.Section);
        services.AddOptions<EmailDeliveryOptions>().BindConfiguration(EmailDeliveryOptions.Section);

        services.AddSingleton<EmailQueue>();
        services.AddSingleton<IEmailSender>(provider => provider.GetRequiredService<EmailQueue>());
        services.AddSingleton<IEmailTransport, SmtpEmailTransport>();
        services.AddHostedService<EmailDispatcher>();

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
            services.AddSingleton(provider => new BlobServiceClient(
                    AttachmentStorageOptions.WithoutContainerName(connectionString), AttachmentStorageOptions.ClientOptions())
                .GetBlobContainerClient(provider.GetRequiredService<IOptions<AttachmentStorageOptions>>().Value.Container));
            services.AddSingleton<IFileStore, BlobFileStore>();
        }

        services.AddSingleton<IMalwareScanner, NoMalwareScanner>();
        services.AddSingleton<IThumbnailGenerator, SkiaThumbnailGenerator>();
        services.AddSingleton<IUploadStaging, TempFileUploadStaging>();
        services.AddHostedService<StaleUploadSweeper>();

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
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPatientPathwayRepository, PatientPathwayRepository>();
        services.AddScoped<INoteDeletionRequestRepository, NoteDeletionRequestRepository>();
        services.AddScoped<IPatientPathwayStagesRepository, PatientPathwayStagesRepository>();
        services.AddScoped<IPathwayAuditRepository, PathwayAuditRepository>();
        services.AddScoped<INotepadRepository, NotepadRepository>();
        services.AddScoped<IPathwayInsightsRepository, PathwayInsightsRepository>();
        services.AddScoped<IUserSettingsRepository, UserSettingsRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<ICalendarSetupRepository, CalendarSetupRepository>();
        services.AddScoped<ICalendarRepository, CalendarRepository>();
        services.AddScoped<IClosedDayRepository, ClosedDayRepository>();
        services.AddScoped<ISiteRepository, SiteRepository>();
        services.AddScoped<IWorkingHoursRepository, WorkingHoursRepository>();
        services.AddScoped<IMeetingRepository, MeetingRepository>();
        services.AddScoped<MedApp.Application.Mdt.Repositories.IMdtItemRepository, MdtItemRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IPatientMessageRepository, PatientMessageRepository>();
        services.AddScoped<IClinicRepository, ClinicRepository>();
        services.AddScoped<ICalendarLock, CalendarLock>();
        services.AddScoped<ISignInChallengeRepository, SignInChallengeRepository>();
        services.AddScoped<IEmailCodeRepository, EmailCodeRepository>();
        services.AddScoped<ISecuritySettingsRepository, SecuritySettingsRepository>();
        services.AddScoped<IAccountLockoutRepository, AccountLockoutRepository>();
        services.AddScoped<INotificationPreferencesRepository, NotificationPreferencesRepository>();
        services.AddScoped<IMonitoringRuleRepository, MonitoringRuleRepository>();
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<IPatientRecordRepository, PatientRecordRepository>();
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();
        services.AddScoped<IScheduledJobRepository, ScheduledJobRepository>();
        services.AddScoped<IOperationalDataRepository, OperationalDataRepository>();
        services.AddScoped<ISpecialtyRepository, SpecialtyRepository>();
        services.AddScoped<IPracticeRepository, PracticeRepository>();
        services.AddScoped<ILetterTemplateRepository, LetterTemplateRepository>();
        services.AddScoped<ILetterRepository, LetterRepository>();
        services.AddScoped<IReferralRepository, ReferralRepository>();
        services.AddScoped<ISpecimenRepository, SpecimenRepository>();
        
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
            .AddRoles<ApplicationRole>()
            // Refuses the most common passwords, beside the length and character rules (Identity settings).
            .AddPasswordValidator<CommonPasswordValidator>()
            .AddEntityFrameworkStores<MedAppDbContext>()
            .AddDefaultTokenProviders()
            // Password reset links: 30 minutes (IdentityOptionsConfiguration points resets at it).
            .AddTokenProvider<PasswordResetTokenProvider>(PasswordResetTokenProvider.ProviderName);

        // Other tokens from the default provider (email confirmation links) last a day.
        services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = EmailTemplates.ConfirmLinkLifetime);

        services.AddScoped<IAuthenticationService, IdentityAuthenticationService>();
        services.AddScoped<ISessionService, SessionService>();

        services.AddScoped<IIdentityReadService, IdentityReadService>();
        services.AddScoped<IIdentityUserService, IdentityUserService>();
        services.AddScoped<IIdentityRoleService, IdentityRoleService>();
        services.AddScoped<IIdentityTransactions, IdentityTransactions>();
        services.AddScoped<ITwoFactorService, TwoFactorService>();
        services.AddScoped<IAccountEmailService, AccountEmailService>();
        services.AddScoped<IStaffDetailsService, StaffDetailsService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddSingleton<IQrCodeRenderer, QrCodeRenderer>();

        services.AddScoped<IdentityUserService>();
        services.AddScoped<IdentityRoleService>();
        services.AddScoped<FirstAdminSetup>();
        services.AddScoped<AdminGrant>();
        services.AddScoped<ILastAdminGuard, LastAdminGuard>();
        services.AddScoped<IPermissionHolders, PermissionHolders>();

        return services;
    }
}