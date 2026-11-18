using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Pathways.PatientPathways;

public sealed class PathwayTemplateConfiguration : IEntityTypeConfiguration<PathwayTemplate>
{
    public void Configure(EntityTypeBuilder<PathwayTemplate> builder)
    {
        builder.ToTable("PathwayTemplates");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.HasMany(x => x.Maps)
            .WithOne(x => x.Template)
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.IsArchived)
            .HasDefaultValue(false);

        // Stored by number; Standard (the adult or child rule) for everything made before classes, and from an API copy
        // that doesn't know them.
        builder.Property(x => x.RetentionClass)
            .HasDefaultValue(RetentionClass.Standard);

        builder.HasIndex(x => x.Name)
            .IsUnique();

        // Required. The database's default is General, so an API copy from before teams can still create a
        // template during a rollout. A team in use is archived, never deleted.
        builder.Property(x => x.TeamId)
            .HasDefaultValue(Team.GeneralId);

        builder.HasOne(x => x.Team)
            .WithMany()
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}