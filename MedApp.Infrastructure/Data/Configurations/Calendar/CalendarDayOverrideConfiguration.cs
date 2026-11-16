using MedApp.Domain.Calendar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Calendar;

public sealed class CalendarDayOverrideConfiguration : IEntityTypeConfiguration<CalendarDayOverride>
{
    public void Configure(EntityTypeBuilder<CalendarDayOverride> builder)
    {
        builder.ToTable("CalendarDayOverrides");

        builder.HasKey(x => x.Id);

        // Day templates in use are archived, never deleted.
        builder.HasOne(x => x.DayTemplate)
            .WithMany()
            .HasForeignKey(x => x.DayTemplateId)
            .OnDelete(DeleteBehavior.NoAction);

        // One per person and date.
        builder.HasIndex(x => new { x.StaffUserId, x.Date })
            .IsUnique();
    }
}
