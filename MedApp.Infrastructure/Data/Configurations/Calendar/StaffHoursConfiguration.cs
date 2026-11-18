using MedApp.Domain.Calendar;
using MedApp.Domain.Sites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Calendar;

public sealed class StaffHoursConfiguration : IEntityTypeConfiguration<StaffHours>
{
    public void Configure(EntityTypeBuilder<StaffHours> builder)
    {
        builder.ToTable("StaffHours");

        builder.HasKey(x => x.Id);

        // .NET's DayOfWeek number (Sunday 0 to Saturday 6), as week template days are stored.
        builder.Property(x => x.DayOfWeek);

        // No foreign key to the users table for StaffUserId, as with default week templates. A site named by anyone's
        // hours is in use, so it can't be deleted, only archived.
        builder.HasOne<Site>()
            .WithMany()
            .HasForeignKey(x => x.SiteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.StaffUserId, x.ValidFrom });
    }
}
