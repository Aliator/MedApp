using MedApp.Domain.Pathways.PatientPathways;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Pathways.PatientPathways;

public sealed class PatientPathwayConfiguration : IEntityTypeConfiguration<PatientPathway>
{
    public void Configure(EntityTypeBuilder<PatientPathway> builder)
    {
        builder.ToTable("PatientPathways");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        // The old single notes field, moved to PatientPathwayNotes. Kept, unused, for API copies from before the move
        // during a rollout; dropped in a later release.
        builder.Property(x => x.LegacyNotes)
            .HasColumnName("Notes")
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(x => x.DueDateUtc)
            .IsRequired();

        builder.Property(x => x.Priority)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.BlockedFromStatus);

        builder.Property(x => x.TemplateName)
            .IsRequired()
            .HasMaxLength(150)
            .HasDefaultValue(string.Empty);

        // The template a pathway came from. Templates in use are archived, never deleted, so no cascade.
        builder.HasOne<PathwayTemplate>()
            .WithMany()
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.LastUpdated)
            .IsRequired();

        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(x => x.Stages)
            .WithOne(x => x.PatientPathway)
            .HasForeignKey(x => x.PatientPathwayId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Assignments)
            .WithOne(x => x.PatientPathway)
            .HasForeignKey(x => x.PatientPathwayId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.PatientId, x.Status });
        builder.HasIndex(x => x.DueDateUtc);
    }
}