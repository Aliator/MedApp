using MedApp.Domain.Tasks.PatientTasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Tasks.PatientTasks;

public sealed class PatientTaskConfiguration : IEntityTypeConfiguration<PatientTask>
{
    public void Configure(EntityTypeBuilder<PatientTask> builder)
    {
        builder.ToTable("PatientTasks");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Notes)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(x => x.DueDateUtc)
            .IsRequired();

        builder.Property(x => x.Priority)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.LastUpdated)
            .IsRequired();

        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(x => x.Stages)
            .WithOne(x => x.PatientTask)
            .HasForeignKey(x => x.PatientTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Assignments)
            .WithOne(x => x.PatientTask)
            .HasForeignKey(x => x.PatientTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.PatientId, x.Status });
        builder.HasIndex(x => x.DueDateUtc);
    }
}