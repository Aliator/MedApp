using MedApp.Domain.Tasks.PatientTask;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations;

public sealed class PatientTaskStageDefinitionConfiguration : IEntityTypeConfiguration<PatientTaskStageDefinition>
{
    public void Configure(EntityTypeBuilder<PatientTaskStageDefinition> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Instructions)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.LastUpdated)
            .IsRequired();

        builder.HasIndex(x => x.Name)
            .IsUnique();
    }
}