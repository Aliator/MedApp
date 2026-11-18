using MedApp.Domain.Pathways.PatientPathways;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Pathways.PatientPathways;

public sealed class PathwayTemplateMapConfiguration : IEntityTypeConfiguration<PathwayTemplateMap>
{
    public void Configure(EntityTypeBuilder<PathwayTemplateMap> builder)
    {
        builder.ToTable("PathwayTemplateMaps");

        // Keyed by position, so a template can list the same stage more than once.
        builder.HasKey(x => new { x.TemplateId, x.StageOrder });

        builder.Property(x => x.StageOrder)
            .IsRequired();

        // At most one per template (the handlers check it).
        builder.Property(x => x.StopsClock)
            .HasDefaultValue(false);

        builder.HasOne(x => x.Template)
            .WithMany(x => x.Maps)
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.StageDefinition)
            .WithMany()
            .HasForeignKey(x => x.StageDefinitionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => x.StageDefinitionId);
    }
}