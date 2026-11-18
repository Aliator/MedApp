using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Terminology;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Pathways.PatientPathways;

public sealed class PatientPathwayStageDefinitionConfiguration : IEntityTypeConfiguration<PatientPathwayStageDefinition>
{
    public void Configure(EntityTypeBuilder<PatientPathwayStageDefinition> builder)
    {
        builder.ToTable("PatientPathwayStageDefinitions");

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

        // A SNOMED CT concept ID (6 to 18 digits) and its display term, both optional (SnomedCt).
        builder.Property(x => x.SnomedCode)
            .HasMaxLength(SnomedCt.MaxLength)
            .IsUnicode(false);

        builder.Property(x => x.SnomedTerm)
            .HasMaxLength(SnomedCt.MaxTermLength);
        
        builder.Property(x => x.IsArchived)
            .HasDefaultValue(false);

        builder.Property(x => x.VisitRequirement)
            .HasDefaultValue(VisitRequirement.None);

        builder.HasIndex(x => x.Name)
            .IsUnique();
        
        builder.HasData(new PatientPathwayStageDefinition
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