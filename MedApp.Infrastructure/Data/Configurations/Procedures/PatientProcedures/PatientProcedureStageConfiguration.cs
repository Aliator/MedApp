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

        builder.HasOne(x => x.PatientProcedure)
            .WithMany(x => x.Stages)
            .HasForeignKey(x => x.PatientProcedureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.StageDefinition)
            .WithMany()
            .HasForeignKey(x => x.StageDefinitionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => new { x.PatientProcedureId, x.StageOrder })
            .IsUnique();

        builder.HasIndex(x => new { x.PatientProcedureId, x.StageDefinitionId })
            .IsUnique();
    }
}