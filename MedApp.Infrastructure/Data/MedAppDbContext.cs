using MedApp.Domain.Activity;
using MedApp.Domain.Attachments;
using MedApp.Domain.Authentication;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Notifications;
using MedApp.Domain.Patients;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data.Conversions;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data;

public sealed class MedAppDbContext(DbContextOptions<MedAppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreferences> NotificationPreferences => Set<NotificationPreferences>();
    public DbSet<MonitoringRule> MonitoringRules => Set<MonitoringRule>();
    public DbSet<PatientProcedure> PatientProcedures => Set<PatientProcedure>();
    public DbSet<PatientProcedureStage> PatientProcedureStages => Set<PatientProcedureStage>();
    public DbSet<PatientProcedureAssignment> PatientProcedureAssignments => Set<PatientProcedureAssignment>();
    public DbSet<PatientProcedureStageDefinition> PatientProcedureStageDefinitions => Set<PatientProcedureStageDefinition>();
    public DbSet<PatientProcedureStageTemplate> PatientProcedureStageTemplates => Set<PatientProcedureStageTemplate>();
    public DbSet<PatientProcedureStageTemplateMap> PatientProcedureStageTemplateItems => Set<PatientProcedureStageTemplateMap>();
    public DbSet<PatientProcedureAuditEntry> PatientProcedureAuditEntries => Set<PatientProcedureAuditEntry>();
    public DbSet<SignInChallenge> SignInChallenges => Set<SignInChallenge>();
    public DbSet<SecuritySettings> SecuritySettings => Set<SecuritySettings>();

    /// <summary>
    /// ASP.NET Core's data protection keys, shared by every copy of the API, so a token one copy seals (a password reset
    /// or email confirmation link) opens on another (<c>DataProtectionKeys</c> in ServiceExtensions).
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<ActivityEntry> ActivityEntries => Set<ActivityEntry>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

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