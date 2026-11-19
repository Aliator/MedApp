using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Infrastructure.Data.Conversions;
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

        builder.Property(x => x.IsOptional)
            .HasDefaultValue(false);

        // A choice node's kind by number (StageChoiceKind), and its outcomes as JSON (MC10b).
        builder.Property(x => x.Outcomes)
            .HasJsonConversion()
            .HasDefaultValueSql("N'[]'");

        // The relay (MC7): the team that works the stage, null for the template's own. Teams in use are archived,
        // never deleted.
        builder.HasOne(x => x.OwnerTeam)
            .WithMany()
            .HasForeignKey(x => x.OwnerTeamId)
            .OnDelete(DeleteBehavior.NoAction);

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