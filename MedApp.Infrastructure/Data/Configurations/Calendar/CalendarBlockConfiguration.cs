using MedApp.Domain.Calendar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Calendar;

public sealed class CalendarBlockConfiguration : IEntityTypeConfiguration<CalendarBlock>
{
    public void Configure(EntityTypeBuilder<CalendarBlock> builder)
    {
        builder.ToTable("CalendarBlocks");

        builder.HasKey(x => x.Id);

        // No foreign key to the users table for StaffUserId, as with procedure assignments.
        builder.Property(x => x.Note)
            .IsRequired()
            .HasMaxLength(200)
            .HasDefaultValue(string.Empty);

        // Activity types in use are archived, never deleted.
        builder.HasOne(x => x.ActivityType)
            .WithMany()
            .HasForeignKey(x => x.ActivityTypeId)
            .OnDelete(DeleteBehavior.NoAction);

        // A calendar's blocks in a range of dates.
        builder.HasIndex(x => new { x.StaffUserId, x.StartUtc });
    }
}
