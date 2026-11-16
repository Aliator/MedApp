using MedApp.Domain.Calendar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Calendar;

public sealed class WeekTemplateDayConfiguration : IEntityTypeConfiguration<WeekTemplateDay>
{
    public void Configure(EntityTypeBuilder<WeekTemplateDay> builder)
    {
        builder.ToTable("WeekTemplateDays", table =>
            table.HasCheckConstraint("CK_WeekTemplateDays_DayOfWeek", "[DayOfWeek] BETWEEN 1 AND 5"));

        builder.HasKey(x => x.Id);

        // Stored as its number: Monday (1) to Friday (5).
        builder.Property(x => x.DayOfWeek)
            .IsRequired();

        builder.HasOne(x => x.WeekTemplate)
            .WithMany(x => x.Days)
            .HasForeignKey(x => x.WeekTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        // Day templates in use are archived, never deleted.
        builder.HasOne(x => x.DayTemplate)
            .WithMany()
            .HasForeignKey(x => x.DayTemplateId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => new { x.WeekTemplateId, x.DayOfWeek, x.ValidFrom });
    }
}
