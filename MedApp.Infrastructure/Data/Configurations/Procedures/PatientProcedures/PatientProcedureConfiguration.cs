using MedApp.Domain.Procedures.PatientProcedures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Procedures.PatientProcedures;

public sealed class PatientProcedureConfiguration : IEntityTypeConfiguration<PatientProcedure>
{
    public void Configure(EntityTypeBuilder<PatientProcedure> builder)
    {
        builder.ToTable("PatientProcedures");

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

        builder.Property(x => x.BlockedFromStatus);

        builder.Property(x => x.TemplateName)
            .IsRequired()
            .HasMaxLength(150)
            .HasDefaultValue(string.Empty);

        // The template a procedure came from. Templates in use are archived, never deleted, so no cascade.
        builder.HasOne<PatientProcedureStageTemplate>()
            .WithMany()
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.LastUpdated)
            .IsRequired();

        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(x => x.Stages)
            .WithOne(x => x.PatientProcedure)
            .HasForeignKey(x => x.PatientProcedureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Assignments)
            .WithOne(x => x.PatientProcedure)
            .HasForeignKey(x => x.PatientProcedureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.PatientId, x.Status });
        builder.HasIndex(x => x.DueDateUtc);
    }
}