using MedApp.Domain.Procedures.PatientProcedures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Procedures.PatientProcedures;

public sealed class PatientProcedureStageDefinitionConfiguration : IEntityTypeConfiguration<PatientProcedureStageDefinition>
{
    public void Configure(EntityTypeBuilder<PatientProcedureStageDefinition> builder)
    {
        builder.ToTable("PatientProcedureStageDefinitions");

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
        
        builder.Property(x => x.IsArchived)
            .HasDefaultValue(false);

        builder.HasIndex(x => x.Name)
            .IsUnique();
        
        builder.HasData(new PatientProcedureStageDefinition
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Definition missing",
            Description = "This stage definition was deleted.",
            Instructions = "No instructions available.",
            // Left over from before definitions were archived instead of deleted; kept for old data, never offered.
            IsArchived = true
        });
    }
}