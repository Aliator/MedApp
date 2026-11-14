using MedApp.Domain.Authentication;
using MedApp.Domain.Patients;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.Infrastructure.Common.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data;

public sealed class MedAppDbContext(DbContextOptions<MedAppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<PatientProcedure> PatientProcedures => Set<PatientProcedure>();
    public DbSet<PatientProcedureStage> PatientProcedureStages => Set<PatientProcedureStage>();
    public DbSet<PatientProcedureAssignment> PatientProcedureAssignments => Set<PatientProcedureAssignment>();
    public DbSet<PatientProcedureStageDefinition> PatientProcedureStageDefinitions => Set<PatientProcedureStageDefinition>();
    public DbSet<PatientProcedureStageTemplate> PatientProcedureStageTemplates => Set<PatientProcedureStageTemplate>();
    public DbSet<PatientProcedureStageTemplateMap> PatientProcedureStageTemplateItems => Set<PatientProcedureStageTemplateMap>();
    public DbSet<PatientProcedureAuditEntry> PatientProcedureAuditEntries => Set<PatientProcedureAuditEntry>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(MedAppDbContext).Assembly);
    }
}