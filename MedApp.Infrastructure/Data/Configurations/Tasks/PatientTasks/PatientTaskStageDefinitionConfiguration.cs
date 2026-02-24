using MedApp.Domain.Tasks.PatientTasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Tasks.PatientTasks;

public sealed class PatientTaskStageDefinitionConfiguration : IEntityTypeConfiguration<PatientTaskStageDefinition>
{
    public void Configure(EntityTypeBuilder<PatientTaskStageDefinition> builder)
    {
        builder.ToTable("PatientTaskStageDefinitions");

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
        
        builder.HasIndex(x => x.Name)
            .IsUnique();
        
        builder.HasData(new PatientTaskStageDefinition
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Definition missing",
            Description = "This stage definition was deleted.",
            Instructions = "No instructions available."
        });
    }
}