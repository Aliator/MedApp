using MedApp.Domain.Calendar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Calendar;

public sealed class WeekTemplateConfiguration : IEntityTypeConfiguration<WeekTemplate>
{
    public void Configure(EntityTypeBuilder<WeekTemplate> builder)
    {
        builder.ToTable("WeekTemplates");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.IsArchived)
            .HasDefaultValue(false);

        // Each member of staff has their own templates (no foreign key to the users table for OwnerUserId), and names
        // are unique per owner.
        builder.HasIndex(x => new { x.OwnerUserId, x.Name })
            .IsUnique();

        // A week template owns its days (every version of them).
        builder.HasMany(x => x.Days)
            .WithOne(x => x.WeekTemplate)
            .HasForeignKey(x => x.WeekTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
