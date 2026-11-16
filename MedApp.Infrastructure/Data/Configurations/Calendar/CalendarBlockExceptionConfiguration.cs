using MedApp.Domain.Calendar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Calendar;

public sealed class CalendarBlockExceptionConfiguration : IEntityTypeConfiguration<CalendarBlockException>
{
    public void Configure(EntityTypeBuilder<CalendarBlockException> builder)
    {
        builder.ToTable("CalendarBlockExceptions");

        builder.HasKey(x => x.Id);

        // Goes with its template block (which goes with its day template); the only cascade path to it.
        builder.HasOne(x => x.DayTemplateBlock)
            .WithMany()
            .HasForeignKey(x => x.DayTemplateBlockId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.StaffUserId, x.Date, x.DayTemplateBlockId })
            .IsUnique();
    }
}
