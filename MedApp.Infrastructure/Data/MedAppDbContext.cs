using MedApp.Domain.Authentication;
using MedApp.Domain.Patients;
using MedApp.Domain.Tasks.PatientTasks;
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
    public DbSet<PatientTask> PatientTasks => Set<PatientTask>();
    public DbSet<PatientTaskStage> PatientTaskStages => Set<PatientTaskStage>();
    public DbSet<PatientTaskAssignment> PatientTaskAssignments => Set<PatientTaskAssignment>();
    public DbSet<PatientTaskStageDefinition> PatientTaskStageDefinitions => Set<PatientTaskStageDefinition>();
    public DbSet<PatientTaskStageTemplate> PatientTaskStageTemplates => Set<PatientTaskStageTemplate>();
    public DbSet<PatientTaskStageTemplateMap> PatientTaskStageTemplateItems => Set<PatientTaskStageTemplateMap>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(MedAppDbContext).Assembly);
    }
}