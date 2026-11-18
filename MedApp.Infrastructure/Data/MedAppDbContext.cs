using MedApp.Domain.Activity;
using MedApp.Domain.Appointments;
using MedApp.Domain.Attachments;
using MedApp.Domain.Authentication;
using MedApp.Domain.Calendar;
using MedApp.Domain.Clinics;
using MedApp.Domain.Jobs;
using MedApp.Domain.Letters;
using MedApp.Domain.Sites;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Notepads;
using MedApp.Domain.Notifications;
using MedApp.Domain.PatientMessages;
using MedApp.Domain.Patients;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Practices;
using MedApp.Domain.Referrals;
using MedApp.Domain.Specialties;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data.Conversions;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data;

public sealed class MedAppDbContext(DbContextOptions<MedAppDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options), IDataProtectionKeyContext
{
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreferences> NotificationPreferences => Set<NotificationPreferences>();
    public DbSet<MonitoringRule> MonitoringRules => Set<MonitoringRule>();
    public DbSet<PatientPathway> PatientPathways => Set<PatientPathway>();
    public DbSet<PatientPathwayStage> PatientPathwayStages => Set<PatientPathwayStage>();
    public DbSet<PatientPathwayAssignment> PatientPathwayAssignments => Set<PatientPathwayAssignment>();
    public DbSet<PatientPathwayStageDefinition> PatientPathwayStageDefinitions => Set<PatientPathwayStageDefinition>();
    public DbSet<PathwayTemplate> PathwayTemplates => Set<PathwayTemplate>();
    public DbSet<PathwayTemplateMap> PathwayTemplateItems => Set<PathwayTemplateMap>();
    public DbSet<PatientPathwayAuditEntry> PatientPathwayAuditEntries => Set<PatientPathwayAuditEntry>();
    public DbSet<PatientPathwayNote> PatientPathwayNotes => Set<PatientPathwayNote>();
    public DbSet<PatientPathwayNoteVersion> PatientPathwayNoteVersions => Set<PatientPathwayNoteVersion>();
    public DbSet<PathwayNoteDeletionRequest> PathwayNoteDeletionRequests => Set<PathwayNoteDeletionRequest>();
    public DbSet<Specialty> Specialties => Set<Specialty>();
    public DbSet<StaffSpecialty> StaffSpecialties => Set<StaffSpecialty>();
    public DbSet<Referral> Referrals => Set<Referral>();
    public DbSet<Practice> Practices => Set<Practice>();
    public DbSet<LetterTemplate> LetterTemplates => Set<LetterTemplate>();
    public DbSet<ReferralEvent> ReferralEvents => Set<ReferralEvent>();
    public DbSet<ReferralClockPause> ReferralClockPauses => Set<ReferralClockPause>();

    /// <summary>Staff's own notepads, one each (deleted with the account).</summary>
    public DbSet<Notepad> Notepads => Set<Notepad>();
    public DbSet<CalendarActivityType> CalendarActivityTypes => Set<CalendarActivityType>();
    public DbSet<CalendarBlock> CalendarBlocks => Set<CalendarBlock>();
    public DbSet<CalendarBlockException> CalendarBlockExceptions => Set<CalendarBlockException>();
    public DbSet<CalendarDayOverride> CalendarDayOverrides => Set<CalendarDayOverride>();
    public DbSet<ClosedDay> ClosedDays => Set<ClosedDay>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<SiteHours> SiteHours => Set<SiteHours>();
    public DbSet<ClinicSession> ClinicSessions => Set<ClinicSession>();
    public DbSet<ClinicPattern> ClinicPatterns => Set<ClinicPattern>();
    public DbSet<StaffHours> StaffHours => Set<StaffHours>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<MeetingAttendee> MeetingAttendees => Set<MeetingAttendee>();
    public DbSet<DayTemplate> DayTemplates => Set<DayTemplate>();
    public DbSet<DayTemplateBlock> DayTemplateBlocks => Set<DayTemplateBlock>();
    public DbSet<WeekTemplate> WeekTemplates => Set<WeekTemplate>();
    public DbSet<WeekTemplateDay> WeekTemplateDays => Set<WeekTemplateDay>();
    public DbSet<StaffWeekTemplate> StaffWeekTemplates => Set<StaffWeekTemplate>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentEvent> AppointmentEvents => Set<AppointmentEvent>();
    public DbSet<PatientMessage> PatientMessages => Set<PatientMessage>();
    public DbSet<SignInChallenge> SignInChallenges => Set<SignInChallenge>();
    public DbSet<EmailCode> EmailCodes => Set<EmailCode>();
    public DbSet<SecuritySettings> SecuritySettings => Set<SecuritySettings>();

    /// <summary>Each role's permissions, by name (the built-in Admin role's are fixed in code, so it has none here).</summary>
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    /// <summary>
    /// ASP.NET Core's data protection keys, shared by every copy of the API, so a token one copy seals (a password reset
    /// or email confirmation link) opens on another (<c>DataProtectionKeys</c> in ServiceExtensions).
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<ActivityEntry> ActivityEntries => Set<ActivityEntry>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<ScheduledJob> ScheduledJobs => Set<ScheduledJob>();

    /// <summary>Times are stored as UTC, and read back marked as UTC (<see cref="UtcDateTimeConverter"/>).</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Covers DateTime? too. A converter between the same types changes no schema (no migration).
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(MedAppDbContext).Assembly);

        InsightsDbFunctions.Register(modelBuilder);
    }
}