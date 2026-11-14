using MedApp.Domain.Procedures.PatientProcedures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Procedures.PatientProcedures;

public sealed class PatientProcedureStageConfiguration : IEntityTypeConfiguration<PatientProcedureStage>
{
    public void Configure(EntityTypeBuilder<PatientProcedureStage> builder)
    {
        builder.ToTable("PatientProcedureStages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StageOrder)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.LastUpdated)
            .IsRequired();

        builder.Property(x => x.CompletedAtUtc);

        // Copied from the stage definition when the procedure is created (same limits as the definition).
        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100)
            .HasDefaultValue(string.Empty);

        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(500)
            .HasDefaultValue(string.Empty);

        builder.Property(x => x.Instructions)
            .IsRequired()
            .HasMaxLength(2000)
            .HasDefaultValue(string.Empty);

        builder.Property(x => x.Notes)
            .IsRequired()
            .HasMaxLength(4000)
            .HasDefaultValue(string.Empty);

        builder.Property(x => x.IsLocked)
            .HasDefaultValue(false);

        builder.HasOne(x => x.PatientProcedure)
            .WithMany(x => x.Stages)
            .HasForeignKey(x => x.PatientProcedureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.StageDefinition)
            .WithMany()
            .HasForeignKey(x => x.StageDefinitionId)
            .OnDelete(DeleteBehavior.NoAction);

        // Unique by position, not by definition: a procedure can have the same stage more than once.
        builder.HasIndex(x => new { x.PatientProcedureId, x.StageOrder })
            .IsUnique();
    }
}