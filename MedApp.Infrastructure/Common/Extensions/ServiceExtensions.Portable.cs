using MedApp.Application.Activity.Repositories;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Attachments.Repositories;
using MedApp.Application.Authentication.Roles.Repositories;
using MedApp.Application.Authentication.Security.Repositories;
using MedApp.Application.Authentication.Settings.Repositories;
using MedApp.Application.Authentication.TwoFactor.Repositories;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Clinics.Repositories;
using MedApp.Application.Consent.Repositories;
using MedApp.Application.Demographics.Common;
using MedApp.Application.Jobs.Common;
using MedApp.Application.Jobs.OperationalPurge;
using MedApp.Application.Jobs.Repositories;
using MedApp.Application.Letters.Repositories;
using MedApp.Application.Monitoring.Repositories;
using MedApp.Application.Notepads.Repositories;
using MedApp.Application.Notifications.Repositories;
using MedApp.Application.PatientMessages.Repositories;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Practices.Repositories;
using MedApp.Application.Referrals.Repositories;
using MedApp.Application.Sites.Repositories;
using MedApp.Application.Specimens.Repositories;
using MedApp.Application.Teams.Repositories;
using MedApp.Infrastructure.Data.Repositories;
using MedApp.Infrastructure.Demographics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MedApp.Infrastructure.Common.Extensions;

/// <summary>
/// The registrations with nothing SQL Server, Azure or ASP.NET Core in them: the repositories, the scheduled jobs'
/// settings and the demographics lookup. The browser build (<c>MedApp.Browser</c>) compiles this file too.
/// </summary>
public static partial class ServiceExtensions
{
    /// <summary>
    /// The scheduled jobs' settings: <c>Jobs:Enabled</c>, each job's <c>Jobs:{Name}:Enabled</c>, and the operational
    /// purge's periods (<c>Retention:Operational</c>), with periods under 1 stopping a host at startup. Whether a timer
    /// runs the jobs is the host's choice (the API's <c>ScheduledJobHost</c>).
    /// </summary>
    internal static IServiceCollection AddJobSettings(this IServiceCollection services, IConfiguration configuration)
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

    internal static IServiceCollection AddRepositories(
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
        services.AddScoped<ISignInChallengeRepository, SignInChallengeRepository>();
        services.AddScoped<IEmailCodeRepository, EmailCodeRepository>();
        services.AddScoped<ISecuritySettingsRepository, SecuritySettingsRepository>();
        services.AddScoped<IAccountLockoutRepository, AccountLockoutRepository>();
        services.AddScoped<INotificationPreferencesRepository, NotificationPreferencesRepository>();
        services.AddScoped<IMonitoringRuleRepository, MonitoringRuleRepository>();
        services.AddScoped<IServiceAlertRepository, ServiceAlertRepository>();
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<IPatientRecordRepository, PatientRecordRepository>();
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();
        services.AddScoped<IScheduledJobRepository, ScheduledJobRepository>();
        services.AddScoped<IOperationalDataRepository, OperationalDataRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IPathwayAccessRepository, PathwayAccessRepository>();
        services.AddScoped<MedApp.Application.Consults.Repositories.IConsultRepository, ConsultRepository>();
        services.AddScoped<IPracticeRepository, PracticeRepository>();
        services.AddScoped<ILetterTemplateRepository, LetterTemplateRepository>();
        services.AddScoped<ILetterRepository, LetterRepository>();
        services.AddScoped<IReferralRepository, ReferralRepository>();
        services.AddScoped<ISpecimenRepository, SpecimenRepository>();
        services.AddScoped<MedApp.Application.Lab.Repositories.ILabTestRepository, LabTestRepository>();
        services.AddScoped<MedApp.Application.Lab.Repositories.ILabCaseRepository, LabCaseRepository>();
        services.AddScoped<MedApp.Application.Hospitals.Repositories.IHospitalRepository, HospitalRepository>();
        services.AddScoped<MedApp.Application.Results.Repositories.IResultRepository, ResultRepository>();
        services.AddScoped<IConsentRepository, ConsentRepository>();
        services.AddScoped<MedApp.Application.DataRequests.Repositories.IDataRequestRepository, DataRequestRepository>();
        
        return services;
    }

    /// <summary>
    /// The demographics lookup (<c>Pds:Mode</c>): none by default, or the offline stub of synthetic 999 numbers. The
    /// real PDS FHIR API isn't wired in; it needs NHS England's onboarding first.
    /// </summary>
    internal static IServiceCollection AddDemographics(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(PdsOptions.Section).Get<PdsOptions>() ?? new PdsOptions();
        if (options.Mode == PdsMode.Offline)
            services.AddSingleton<IPersonalDemographicsService, OfflinePersonalDemographicsService>();
        else
            services.AddSingleton<IPersonalDemographicsService, NoPersonalDemographicsService>();

        return services;
    }
}
