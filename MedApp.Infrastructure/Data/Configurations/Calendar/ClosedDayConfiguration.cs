using MedApp.Application.Calendar.Common;
using MedApp.Domain.Calendar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Calendar;

public sealed class ClosedDayConfiguration : IEntityTypeConfiguration<ClosedDay>
{
    /// <summary>The unique index that allows one closed day per date.</summary>
    public const string DateIndex = "IX_ClosedDays_Date";

    public void Configure(EntityTypeBuilder<ClosedDay> builder)
    {
        builder.ToTable("ClosedDays");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(CalendarLimits.MaxClosedDayNameLength);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        // No foreign key to the users table (who added it), as with other staff IDs.

        // One per date, and a calendar's closed days are read by date range. A violation is recognised by the index's
        // name (ClosedDayRepository).
        builder.HasIndex(x => x.Date, DateIndex)
            .IsUnique();
    }
}
