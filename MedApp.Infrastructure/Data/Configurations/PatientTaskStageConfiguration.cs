using MedApp.Domain.Tasks.PatientTask;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations;

public sealed class PatientTaskStageConfiguration : IEntityTypeConfiguration<PatientTaskStage>
{
    public void Configure(EntityTypeBuilder<PatientTaskStage> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.StageOrder)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.LastUpdated)
            .IsRequired();

        builder.HasOne(x => x.PatientTask)
            .WithMany(x => x.Stages)
            .HasForeignKey(x => x.PatientTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.StageDefinition)
            .WithMany()
            .HasForeignKey(x => x.StageDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.PatientTaskId, x.StageOrder })
            .IsUnique();

        builder.HasIndex(x => new { x.PatientTaskId, x.StageDefinitionId })
            .IsUnique();

        builder.HasIndex(x => new { x.PatientTaskId, x.Id });
    }
}