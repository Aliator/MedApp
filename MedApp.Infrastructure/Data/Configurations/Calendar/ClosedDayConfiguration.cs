using MedApp.Application.Calendar.Common;
using MedApp.Domain.Calendar;
using MedApp.Domain.Sites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Calendar;

public sealed class ClosedDayConfiguration : IEntityTypeConfiguration<ClosedDay>
{
    /// <summary>The unique index that allows one closed day everywhere per date, and one per site per date.</summary>
    public const string DateAndSiteIndex = "IX_ClosedDays_Date_SiteId";

    public void Configure(EntityTypeBuilder<ClosedDay> builder)
    {
        builder.ToTable("ClosedDays");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(CalendarLimits.MaxClosedDayNameLength);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        // No foreign key to the users table (who added it), as with other staff IDs. A site's closed day keeps its
        // site: a site with closed days can't be deleted, only archived.
        builder.HasOne<Site>()
            .WithMany()
            .HasForeignKey(x => x.SiteId)
            .OnDelete(DeleteBehavior.Restrict);

        // One everywhere per date and one per site per date, and a calendar's closed days are read by date range. EF
        // would filter a unique index on a nullable column to its non-null rows, which would allow two days closed
        // everywhere on one date: without the filter, SQL Server treats the nulls as equal, so it doesn't. A violation
        // is recognised by the index's name (ClosedDayRepository).
        builder.HasIndex(x => new { x.Date, x.SiteId }, DateAndSiteIndex)
            .IsUnique()
            .HasFilter(null);
    }
}
