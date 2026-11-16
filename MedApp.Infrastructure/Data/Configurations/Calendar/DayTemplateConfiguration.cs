using MedApp.Domain.Calendar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Calendar;

public sealed class DayTemplateConfiguration : IEntityTypeConfiguration<DayTemplate>
{
    public void Configure(EntityTypeBuilder<DayTemplate> builder)
    {
        builder.ToTable("DayTemplates");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.IsArchived)
            .HasDefaultValue(false);

        builder.HasIndex(x => x.Name)
            .IsUnique();

        // A day template owns its blocks (every version of them).
        builder.HasMany(x => x.Blocks)
            .WithOne(x => x.DayTemplate)
            .HasForeignKey(x => x.DayTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
