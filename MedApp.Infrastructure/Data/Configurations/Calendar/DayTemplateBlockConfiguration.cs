using MedApp.Domain.Calendar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Calendar;

public sealed class DayTemplateBlockConfiguration : IEntityTypeConfiguration<DayTemplateBlock>
{
    public void Configure(EntityTypeBuilder<DayTemplateBlock> builder)
    {
        builder.ToTable("DayTemplateBlocks");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Note)
            .IsRequired()
            .HasMaxLength(200)
            .HasDefaultValue(string.Empty);

        builder.HasOne(x => x.DayTemplate)
            .WithMany(x => x.Blocks)
            .HasForeignKey(x => x.DayTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ActivityType)
            .WithMany()
            .HasForeignKey(x => x.ActivityTypeId)
            .OnDelete(DeleteBehavior.NoAction);

        // A template's blocks valid on a date.
        builder.HasIndex(x => new { x.DayTemplateId, x.ValidFrom });
    }
}
