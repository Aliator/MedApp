using MedApp.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data;

public sealed class MedAppDbContext(DbContextOptions<MedAppDbContext> options) : DbContext(options)
{
    public DbSet<Patient> Patients => Set<Patient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MedAppDbContext).Assembly);
    }
}