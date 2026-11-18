using MedApp.Domain.Letters;
using MedApp.Domain.Specialties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Letters;

public sealed class LetterTemplateConfiguration : IEntityTypeConfiguration<LetterTemplate>
{
    /// <summary>The unique index on (specialty, name), named as EF names it.</summary>
    public const string NameIndex = "IX_LetterTemplates_SpecialtyId_Name";

    public void Configure(EntityTypeBuilder<LetterTemplate> builder)
    {
        builder.ToTable("LetterTemplates");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(LetterTemplate.MaxNameLength);

        // Stored by number (LetterKind is never renumbered).
        builder.Property(x => x.Kind).IsRequired();

        // A specialty in use is archived, never deleted (SpecialtyUsage counts letter templates).
        builder.HasOne<Specialty>()
            .WithMany()
            .HasForeignKey(x => x.SpecialtyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Names are unique within a specialty, ignoring case (the column's collation). SQL Server's unique index treats
        // nulls as equal, so this also keeps the names of the templates for every specialty unique.
        builder.HasIndex(x => new { x.SpecialtyId, x.Name })
            .IsUnique()
            .HasDatabaseName(NameIndex);

        builder.Property(x => x.Letterhead)
            .IsRequired()
            .HasMaxLength(LetterTemplate.MaxLetterheadLength);

        // nvarchar(max): 20,000 characters is over nvarchar's 4,000 limit; the validators enforce it.
        builder.Property(x => x.BodySkeleton).IsRequired();

        builder.Property(x => x.Closing)
            .IsRequired()
            .HasMaxLength(LetterTemplate.MaxClosingLength);

        builder.Property(x => x.IsArchived)
            .HasDefaultValue(false);

        // Read back with each change: a save from a copy read before another is refused (409).
        builder.Property(x => x.Version)
            .IsRequired()
            .IsConcurrencyToken();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.LastUpdated).IsRequired();
    }
}
