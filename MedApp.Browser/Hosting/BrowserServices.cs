using MedApp.API.Common.Authentication;
using MedApp.Application.Activity.Common;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Extensions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Jobs.Common;
using MedApp.Application.Letters.Common;
using MedApp.Application.Notepads.Common;
using MedApp.Application.Notifications.Common;
using MedApp.Application.PatientMessages.Common;
using MedApp.Application.Patients.Common;
using MedApp.Application.Practices.Common;
using MedApp.Application.Specimens.Common;
using MedApp.Browser.Data;
using MedApp.Browser.Infrastructure;
using MedApp.Infrastructure.Attachments;
using MedApp.Infrastructure.Common.Email;
using MedApp.Infrastructure.Common.Extensions;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data;
using MedApp.Infrastructure.Letters;
using MedApp.Infrastructure.PatientMessages;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace MedApp.Browser.Hosting;

/// <summary>
/// The API's services as <c>Program</c> adds them (<c>AddApi</c>, <c>AddApplication</c>, <c>AddInfrastructure</c>),
/// with the browser's stand-ins where the API's can't run: SQLite for SQL Server, the one in-memory session, files in
/// the database, and no email, texts, secure email, live notifications, thumbnails or locks.
/// </summary>
public static class BrowserServices
{
    /// <param name="settings">Settings over the API's <c>appsettings.json</c>.</param>
    public static IServiceCollection AddBrowserBackend(
        this IServiceCollection services, BrowserDatabase database, IReadOnlyDictionary<string, string?>? settings = null)
    {
        var configuration = Configuration(settings ?? new Dictionary<string, string?>());
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(logging => logging.AddProvider(new BrowserConsoleLoggerProvider()));
        services.AddSingleton(database);

        services.AddApplication();
        services.AddBrowserApi(configuration);
        services.AddBrowserInfrastructure(configuration, database);
        return services;
    }

    /// <summary>
    /// The browser's database: SQLite, the model fitted to it, and SQL Server's dialect translated. EF Core finds the
    /// model's compiled form (<c>Data/CompiledModel</c>, made by <c>tools/MedApp.BrowserDb/optimize-model.sh</c>) itself,
    /// and builds the model only for making a database (<c>EnsureCreated</c>).
    /// </summary>
    public static DbContextOptionsBuilder UseBrowserDatabase(this DbContextOptionsBuilder options, string connectionString)
    {
        options
            .UseSqlite(connectionString)
            .ReplaceService<IModelCustomizer, SqliteModelCustomizer>()
            .AddInterceptors(new SqlServerDialect())
            .ConfigureWarnings(MedApp.Infrastructure.Common.Extensions.ServiceExtensions.LogFailedSavesAtDebug);
        SqliteInsightsTranslations.AddTo(options);
        return options;
    }

    /// <summary>The API's <c>appsettings.json</c>, as it's published, with any settings over it.</summary>
    private static IConfiguration Configuration(IReadOnlyDictionary<string, string?> overrides)
    {
        using var settings = typeof(BrowserServices).Assembly.GetManifestResourceStream("MedApp.Browser.appsettings.json")!;
        var bytes = new MemoryStream();
        settings.CopyTo(bytes);
        bytes.Position = 0;
        return new ConfigurationBuilder().AddJsonStream(bytes).AddInMemoryCollection(overrides).Build();
    }

    /// <summary>What the API's <c>AddApi</c> provides the Application layer.</summary>
    private static void AddBrowserApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<INotificationPublisher, NoNotificationPublisher>();
        services.AddSingleton<ISessionCookieService, BrowserSessionCookieService>();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddScoped<IActivityContext>(provider => provider.GetRequiredService<ScheduledJobScope>().IsJob
            ? SystemActivityContext.Instance
            : new BrowserActivityContext(provider.GetRequiredService<IHttpContextAccessor>()));

        services.AddOptions<ActivityOptions>().BindConfiguration(ActivityOptions.Section);
        services.AddOptions<RetentionOptions>().BindConfiguration(RetentionOptions.Section);
        services.AddOptions<LettersOptions>().BindConfiguration(LettersOptions.Section);
        services.AddOptions<NotepadOptions>().BindConfiguration(NotepadOptions.Section);
        services.AddOptions<SpecimenOptions>().BindConfiguration(SpecimenOptions.Section);
    }

    /// <summary>What Infrastructure's <c>AddInfrastructure</c> registers, with the browser's stand-ins.</summary>
    private static void AddBrowserInfrastructure(this IServiceCollection services, IConfiguration configuration, BrowserDatabase database)
    {
        services.AddDbContext<MedAppDbContext>(options => options.UseBrowserDatabase(database.ConnectionString));

        services.AddBrowserIdentity(configuration);

        services.AddOptions<EmailOptions>().BindConfiguration(EmailOptions.Section);
        services.AddOptions<EmailDeliveryOptions>().BindConfiguration(EmailDeliveryOptions.Section);
        services.AddSingleton<IEmailSender, NoEmailSender>();

        services.AddRepositories();
        services.Replace(ServiceDescriptor.Scoped<ICalendarLock, BrowserCalendarLock>());

        services.AddSingleton<IFileStore, SqliteFileStore>();
        services.AddSingleton<IMalwareScanner, NoMalwareScanner>();
        services.AddSingleton<IThumbnailGenerator, NoThumbnails>();
        services.AddSingleton<IUploadStaging, MemoryUploadStaging>();

        services.AddDemographics(configuration);
        services.AddJobSettings(configuration);

        services.AddOptions<PatientMessageOptions>().BindConfiguration(PatientMessageOptions.Section);
        services.AddOptions<ReminderOptions>().BindConfiguration(ReminderOptions.Section);
        services.AddSingleton<IPatientMessageTransport, NoPatientMessageTransport>();
        services.AddSingleton<ILetterRenderer, PdfSharpLetterRenderer>();
        services.AddSingleton<ILetterTransport, NoLetterTransport>();
    }

    /// <summary>Infrastructure's <c>AddIdentity</c>, with Identity's own token providers for data protection's, and sign-in by choosing.</summary>
    private static void AddBrowserIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<IdentitySettings>(configuration.GetSection("Identity"));
        services.AddSingleton<IConfigureOptions<IdentityOptions>, IdentityOptionsConfiguration>();

        services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddPasswordValidator<CommonPasswordValidator>()
            .AddEntityFrameworkStores<MedAppDbContext>()
            .AddTokenProvider<PasswordResetTokenProvider>(TokenOptions.DefaultProvider)
            .AddTokenProvider<EmailTokenProvider<ApplicationUser>>(TokenOptions.DefaultEmailProvider)
            .AddTokenProvider<PhoneNumberTokenProvider<ApplicationUser>>(TokenOptions.DefaultPhoneProvider)
            .AddTokenProvider<AuthenticatorTokenProvider<ApplicationUser>>(TokenOptions.DefaultAuthenticatorProvider)
            .AddTokenProvider<PasswordResetTokenProvider>(PasswordResetTokenProvider.ProviderName);

        services.AddScoped<IAuthenticationService, BrowserAuthenticationService>();
        services.AddScoped<ISessionService, MedApp.Infrastructure.Common.Authentication.SessionService>();

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
    }
}

/// <summary>Warnings and errors to the browser's console: the runtime has no thread for Microsoft's console logger.</summary>
public sealed class BrowserConsoleLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new Logger(categoryName);

    public void Dispose()
    {
    }

    private sealed class Logger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
                Console.WriteLine($"{logLevel} {category}: {formatter(state, exception)}{(exception is null ? string.Empty : " " + exception)}");
        }
    }
}
