using MedApp.Domain.Pathways.PatientPathways;
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

        builder.HasIndex(x => x.Name)
            .IsUnique();
    }
}